/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meta.XR.MRUtilityKit.Tests
{
    public class SpaceMapGPUTests : MRUKTestBase
    {
        private SpaceMapGPU SpaceMapGPU;
        private SpaceMapGPUTestHelper SpaceMapGPUTestHelper;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // batchmode (or better -nographics) will not have a GPU
            // and compute shaders only run on GPU's
            if (Application.isBatchMode)
            {
                yield return true;
                yield break;
            }
            yield return LoadScene("Packages/com.meta.xr.mrutilitykit/Tests/SpaceMapGPUTests.unity");
            SpaceMapGPU = Object.FindAnyObjectByType<SpaceMapGPU>();
            SpaceMapGPUTestHelper = Object.FindAnyObjectByType<SpaceMapGPUTestHelper>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadScene();
        }

        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator CheckRoom1()
        {
            // batchmode (or better -nographics) will not have a GPU
            // and compute shaders only run on GPU's
            if (!Application.isBatchMode)
            {
                yield return CheckRoom(0);
            }
            yield return null;
        }

        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator CheckRoom2()
        {
            // batchmode (or better -nographics) will not have a GPU
            // and compute shaders only run on GPU's
            if (!Application.isBatchMode)
            {
                yield return CheckRoom(1);
            }
            yield return null;
        }


        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator CheckRoom1WithUpdate()
        {
            // batchmode (or better -nographics) will not have a GPU
            // and compute shaders only run on GPU's
            if (!Application.isBatchMode)
            {
                yield return CheckRoom(0, true);
            }
            yield return null;
        }

        private IEnumerator CheckRoom(int index, bool updateAnchors = false)
        {
            MRUK.Instance.SceneSettings.RoomIndex = index;
            yield return LoadSceneFromJsonStringAndWait(MRUK.Instance.SceneSettings.SceneJsons[MRUK.Instance.SceneSettings.RoomIndex].text);
            SpaceMapGPU = SetupSpaceMapGPU();

            SpaceMapGPU.StartSpaceMap(MRUK.RoomFilter.AllRooms);

            //  Uncomment this code to regenerate the snapshots
            /*
            var bytes = SpaceMapGPU.OutputTexture.EncodeToPNG();
            var path = System.IO.Path.GetFullPath("Packages/com.meta.xr.mrutilitykit/Tests/Textures/Texture.png");
            System.IO.File.WriteAllBytes(path, bytes);
            */

            CompareTextures(index == 0 ? SpaceMapGPUTestHelper.Room : SpaceMapGPUTestHelper.RoomLessAnchors, SpaceMapGPU.OutputTexture);

            if (updateAnchors)
            {
                index = 1;
                MRUK.Instance.SceneSettings.RoomIndex = index;
                yield return LoadSceneFromJsonStringAndWait(MRUK.Instance.SceneSettings.SceneJsons[MRUK.Instance.SceneSettings.RoomIndex].text);
                SpaceMapGPU = SetupSpaceMapGPU();

                SpaceMapGPU.StartSpaceMap(MRUK.RoomFilter.AllRooms);

                CompareTextures(SpaceMapGPUTestHelper.RoomLessAnchors, SpaceMapGPU.OutputTexture);
            }
        }


        private SpaceMapGPU SetupSpaceMapGPU()
        {
            var spaceMap = Object.FindAnyObjectByType<SpaceMapGPU>();
            if (spaceMap == null)
            {
                Assert.Fail();
            }
            return spaceMap;
        }

        private void CompareTextures(Texture2D expected, Texture2D actual, bool checkAlpha = false)
        {
            var colorsExpected = expected.GetPixels();
            var colorsActual = actual.GetPixels();

            Assert.AreEqual(colorsExpected.Length, colorsActual.Length, "Number of pixels");

            for (var i = 0; i < colorsActual.Length; i++)
            {
                if (Mathf.Abs(colorsExpected[i].r - colorsActual[i].r) > 0.01f ||
                    Mathf.Abs(colorsExpected[i].g - colorsActual[i].g) > 0.01f ||
                    Mathf.Abs(colorsExpected[i].b - colorsActual[i].b) > 0.01f ||
                    (checkAlpha && Mathf.Abs(colorsExpected[i].a - colorsActual[i].a) > 0.01f))
                {
                    int x = i % expected.width;
                    int y = i / expected.width;
                    Assert.Fail($"Pixel at ({x}, {y}) colors don't match. Expected {colorsExpected[i]} but got {colorsActual[i]}");
                }
            }
        }
    }
}


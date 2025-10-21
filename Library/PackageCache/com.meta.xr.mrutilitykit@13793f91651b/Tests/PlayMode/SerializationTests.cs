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


using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Meta.XR.MRUtilityKit.Tests
{
    public class SerializationTests : MRUKTestBase
    {
        protected JSONTestHelper _jsonTestHelper;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadScene("Packages/com.meta.xr.mrutilitykit/Tests/SerializationTests.unity", false);
            _jsonTestHelper = Object.FindAnyObjectByType<JSONTestHelper>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadScene();
        }

        /// <summary>
        /// Test that deserialization from the Unity coordinate system works as expected.
        /// </summary>
        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator DeserializationFromUnity()
        {
            yield return ValidateLoadedScene(_jsonTestHelper.UnityExpectedSerializedScene.text);
        }

        /// <summary>
        /// Test that deserialization from the Unreal coordinate system works as expected.
        /// </summary>
        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator DeserializationFromUnreal()
        {
            yield return ValidateLoadedScene(_jsonTestHelper.UnrealExpectedSerializedScene.text);
        }

        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator ClearSceneTest()
        {
            yield return LoadSceneFromJsonStringAndWait(_jsonTestHelper.UnityExpectedSerializedScene.text);
            Assert.AreEqual(1, MRUK.Instance.Rooms.Count);
            MRUK.Instance.ClearScene();
            Assert.AreEqual(0, MRUK.Instance.Rooms.Count);
        }

        IEnumerator ValidateLoadedScene(string sceneJson)
        {
            yield return LoadSceneFromJsonStringAndWait(sceneJson);
            Assert.NotNull(MRUK.Instance.GetCurrentRoom());
            var loadedRoom = MRUK.Instance.GetCurrentRoom();
            yield return LoadSceneFromPrefabAndWait(MRUK.Instance.SceneSettings.RoomPrefabs[0], false);
            var expectedRoom = MRUK.Instance.Rooms[1];
            Assert.IsNotNull(expectedRoom);
            var loadedAnchors = loadedRoom.Anchors;
            var expectedAnchors = expectedRoom.Anchors;
            Assert.AreEqual(expectedAnchors.Count, loadedAnchors.Count);
            for (int i = 0; i < loadedAnchors.Count; i++)
            {
                var loadedAnchor = loadedAnchors[i];
                var expectedAnchor = expectedAnchors[i];
                // Skip UUID check as they could change every time
                if (loadedAnchor.PlaneRect.HasValue)
                {
                    Assert.That(loadedAnchor.PlaneRect.Value.position, Is.EqualTo(expectedAnchor.PlaneRect.Value.position).Using(Vector2EqualityComparer.Instance), "Plane rect position");
                    Assert.That(loadedAnchor.PlaneRect.Value.size, Is.EqualTo(expectedAnchor.PlaneRect.Value.size).Using(Vector2EqualityComparer.Instance), "Plane rect size");
                }

                if (loadedAnchor.VolumeBounds.HasValue)
                {
                    Assert.That(loadedAnchor.VolumeBounds.Value.extents, Is.EqualTo(expectedAnchor.VolumeBounds.Value.extents).Using(Vector3EqualityComparer.Instance), "Volume bounds extents");
                    Assert.That(loadedAnchor.VolumeBounds.Value.center, Is.EqualTo(expectedAnchor.VolumeBounds.Value.center).Using(Vector3EqualityComparer.Instance), "Volume bounds center");
                }

                Assert.That(loadedAnchor.transform.position, Is.EqualTo(expectedAnchor.transform.position).Using(Vector3EqualityComparer.Instance), "Anchor position");
                Assert.That(loadedAnchor.transform.rotation.eulerAngles, Is.EqualTo(expectedAnchor.transform.rotation.eulerAngles).Using(Vector3EqualityComparer.Instance), "Anchor rotation");
                Assert.That(loadedAnchor.transform.localScale, Is.EqualTo(expectedAnchor.transform.localScale).Using(Vector3EqualityComparer.Instance), "Anchor scale");
                Assert.That(loadedAnchor.GetAnchorCenter(), Is.EqualTo(expectedAnchor.GetAnchorCenter()).Using(Vector3EqualityComparer.Instance), "Anchor center");
                if (loadedAnchor.PlaneBoundary2D != null)
                {
                    var loadedPlaneBoundary2D = loadedAnchor.PlaneBoundary2D;
                    var expectedPlaneBoundary2D = expectedAnchor.PlaneBoundary2D;
                    Assert.That(loadedPlaneBoundary2D, Is.EqualTo(expectedPlaneBoundary2D).Using(Vector2EqualityComparer.Instance), "Plane boundary");
                }

                Assert.AreEqual(expectedAnchor.Label, loadedAnchor.Label, "Anchor label");
                var loadedBoundsFaceCenters = loadedAnchor.GetBoundsFaceCenters();
                var expectedBoundsFaceCenters = expectedAnchor.GetBoundsFaceCenters();
                Assert.That(loadedBoundsFaceCenters, Is.EqualTo(expectedBoundsFaceCenters).Using(Vector3EqualityComparer.Instance), "Anchor face centers");
            }
        }

        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator SerializeToProtobufMeshFilter()
        {
            yield return LoadSceneFromJsonStringAndWait(_jsonTestHelper.HierarchyObjects.text);

            var jsonAll = MRUK.Instance.SaveSceneToJsonString(true);
            var jsonNoMesh = MRUK.Instance.SaveSceneToJsonString(false);

            Assert.True(jsonAll.Contains("triangleMeshMETA"), "jsonAll should contain the global mesh");
            Assert.False(jsonNoMesh.Contains("triangleMeshMETA"), "jsonNoMesh not contain the global mesh");
        }

        [UnityTest]
        [Timeout(DefaultTimeoutMs)]
        public IEnumerator SerializeToProtobufRoomFilter()
        {
            MRUK.Instance.ClearScene();
            yield return LoadSceneFromJsonStringAndWait(_jsonTestHelper.SceneWithRoom1Room3.text);

            Assert.AreEqual(2, MRUK.Instance.Rooms.Count, "Expecting 2 rooms");

            var jsonAll = MRUK.Instance.SaveSceneToJsonString(true, null);
            var jsonRoom0 = MRUK.Instance.SaveSceneToJsonString(true, new List<MRUKRoom> { MRUK.Instance.Rooms[0] });
            var jsonRoom1 = MRUK.Instance.SaveSceneToJsonString(true, new List<MRUKRoom> { MRUK.Instance.Rooms[1] });
            var jsonRoom0And1 = MRUK.Instance.SaveSceneToJsonString(true, new List<MRUKRoom> { MRUK.Instance.Rooms[0], MRUK.Instance.Rooms[1] });

            var room0Uuid = Utilities.ReverseGuidByteOrder(MRUK.Instance.Rooms[0].Anchor.Uuid).ToString();
            var room1Uuid = Utilities.ReverseGuidByteOrder(MRUK.Instance.Rooms[1].Anchor.Uuid).ToString();

            Assert.True(jsonAll.Contains(room0Uuid), "jsonAll should contain room 0");
            Assert.True(jsonAll.Contains(room1Uuid), "jsonAll should contain room 1");
            Assert.True(jsonRoom0.Contains(room0Uuid), "jsonRoom0 should contain room 0");
            Assert.False(jsonRoom0.Contains(room1Uuid), "jsonRoom0 should not contain room 1");
            Assert.False(jsonRoom1.Contains(room0Uuid), "jsonRoom1 should not contain room 0");
            Assert.True(jsonRoom1.Contains(room1Uuid), "jsonRoom1 should contain room 1");
            Assert.True(jsonRoom0And1.Contains(room0Uuid), "jsonRoom0And1 should contain room 0");
            Assert.True(jsonRoom0And1.Contains(room1Uuid), "jsonRoom0And1 should contain room 1");

            Assert.AreEqual(jsonAll, jsonRoom0And1, "jsonAll should be the same as jsonRoom0And1");
        }
    }

}


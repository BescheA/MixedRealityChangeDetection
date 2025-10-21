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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;


namespace Meta.XR.MRUtilityKit.Tests
{
    public class MRUKTestBase
    {
        protected const int DefaultTimeoutMs = 10000;
        protected const string EmptyScene = "Packages/com.meta.xr.mrutilitykit/Tests/Empty.unity";

        protected IEnumerator LoadScene(string sceneToLoad, bool awaitMRUKInit = true)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(sceneToLoad,
                new LoadSceneParameters(LoadSceneMode.Single));
            if (awaitMRUKInit && MRUK.Instance != null)
            {
                yield return new WaitUntil(() => MRUK.Instance.IsInitialized);
            }
        }

        protected IEnumerator UnloadScene()
        {
            // Loading an empty scene as single will unload all other scenes
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(EmptyScene,
                new LoadSceneParameters(LoadSceneMode.Single));
        }

        protected IEnumerator LoadSceneFromJsonStringAndWait(string sceneJson)
        {
            // Loading from JSON is an async operation in the shared library so wait
            // until the task completes before continuing
            var result = MRUK.Instance.LoadSceneFromJsonString(sceneJson);
            yield return new WaitUntil(() => result.IsCompleted);
            Assert.AreEqual(MRUK.LoadDeviceResult.Success, result.Result, "Failed to load scene from json string");
        }

        protected IEnumerator LoadSceneFromPrefabAndWait(GameObject scenePrefab, bool clearSceneFirst = true)
        {
            // Loading from prefab is an async operation in the shared library so wait
            // until the task completes before continuing
            var result = MRUK.Instance.LoadSceneFromPrefab(scenePrefab, clearSceneFirst);
            yield return new WaitUntil(() => result.IsCompleted);
            Assert.AreEqual(MRUK.LoadDeviceResult.Success, result.Result, "Failed to load scene from prefab");
        }
    }
}


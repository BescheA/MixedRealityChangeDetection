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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AOT;
using Unity.Collections;
using UnityEngine.Android;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Meta.XR.MRUtilityKit
{
    partial class MRUK
    {
        private OVRTask<LoadDeviceResult>? _loadSceneTask;
        private UInt64 _currentAppSpace;
        private bool _openXrInitialised;

        private static bool IsOpenXRAvailable
        {
            get
            {
#if UNITY_EDITOR && OVRPLUGIN_TESTING
                // When OVRPLUGIN_TESTING is defined OVRPlugin.initialized will return true even know it is
                // not really initialized which can cause issues with the tests so we force it to false here.
                return false;
#else
                return OVRPlugin.initialized;
#endif
            }
        }

        private void InitializeAnchorStore()
        {
            if (IsOpenXRAvailable)
            {
                var xrInstance = OVRPlugin.GetNativeOpenXRInstance();
                var xrSession = OVRPlugin.GetNativeOpenXRSession();
                var xrInstanceProcAddrFunc = OVRPlugin.GetOpenXRInstanceProcAddrFunc();
                _currentAppSpace = OVRPlugin.GetAppSpace();
                if (MRUKNativeFuncs.AnchorStoreCreate(xrInstance, xrSession, xrInstanceProcAddrFunc, _currentAppSpace, null, 0) != MRUKNativeFuncs.MrukResult.Success)
                {
                    Debug.LogError("Failed to create anchor store");
                }
                else
                {
                    _openXrInitialised = true;
                }

                if (OVRPlugin.RegisterOpenXREventHandler(OnOpenXrEvent) != OVRPlugin.Result.Success)
                {
                    Debug.LogError("Failed to register OpenXR event handler");
                }
            }
            else
            {
                MRUKNativeFuncs.AnchorStoreCreateWithoutOpenXr();
            }

            MRUKNativeFuncs.MrukEventListener listener;
            listener.userContext = IntPtr.Zero;
            listener.onPreRoomAnchorAdded = OnPreRoomAnchorAdded;
            listener.onRoomAnchorAdded = OnRoomAnchorAdded;
            listener.onRoomAnchorUpdated = OnRoomAnchorUpdated;
            listener.onRoomAnchorRemoved = OnRoomAnchorRemoved;
            listener.onSceneAnchorAdded = OnSceneAnchorAdded;
            listener.onSceneAnchorUpdated = OnSceneAnchorUpdated;
            listener.onSceneAnchorRemoved = OnSceneAnchorRemoved;
            listener.onDiscoveryFinished = OnDiscoveryFinished;
            listener.onEnvironmentRaycasterCreated = OnEnvironmentRaycasterCreated;
            MRUKNativeFuncs.AnchorStoreRegisterEventListener(listener);
            MRUKNativeFuncs.SetTrackingSpacePoseGetter(GetTrackingSpacePose);
            MRUKNativeFuncs.SetTrackingSpacePoseSetter(SetTrackingSpacePose);
        }

        private void DestroyAnchorStore()
        {
            if (IsOpenXRAvailable)
            {
                OVRPlugin.UnregisterOpenXREventHandler(OnOpenXrEvent);
            }
            MRUKNativeFuncs.AnchorStoreDestroy();
        }

        private void UpdateAnchorStore()
        {
            if (IsOpenXRAvailable)
            {
                var appSpace = OVRPlugin.GetAppSpace();
                if (appSpace != _currentAppSpace)
                {
                    MRUKNativeFuncs.AnchorStoreSetBaseSpace(appSpace);
                    _currentAppSpace = appSpace;
                }
            }
            else if (_openXrInitialised)
            {
                MRUKNativeFuncs.AnchorStoreShutdownOpenXr();
                _openXrInitialised = false;
            }
            // Convert from seconds to nanoseconds
            UInt64 predictedDisplayTime = OVRPlugin.initialized ? (UInt64)(OVRPlugin.GetPredictedDisplayTime() * 1e9) : 0;
            MRUKNativeFuncs.AnchorStoreTick(predictedDisplayTime);
        }

        private async Task<LoadDeviceResult> LoadSceneFromDeviceSharedLib(bool requestSceneCaptureIfNoDataFound, bool removeMissingRooms, SharedRoomsData? sharedRoomsData = null)
        {
            if (_loadSceneTask != null)
            {
                return LoadDeviceResult.DiscoveryOngoing;
            }

            if (!IsOpenXRAvailable)
            {
                return LoadDeviceResult.NotInitialized;
            }

            var sceneModel = MRUKNativeFuncs.MrukSceneModel.V1;
            MRUKNativeFuncs.MrukResult discoverResult;
            if (sharedRoomsData is { } roomsData)
            {
                unsafe
                {
                    using OVRNativeList<Guid> roomUuidsList = roomsData.roomUuids?.ToNativeList(Allocator.Temp) ?? new OVRNativeList<Guid>();
                    var nativeSharedRoomsData = new MRUKNativeFuncs.MrukSharedRoomsData
                    {
                        groupUuid = roomsData.groupUuid,
                        roomUuids = roomUuidsList.Data,
                        numRoomUuids = (uint)roomUuidsList.Count,
                    };

                    if (roomsData.alignmentData.HasValue)
                    {
                        nativeSharedRoomsData.alignmentRoomUuid = roomsData.alignmentData.Value.alignmentRoomUuid;
                        nativeSharedRoomsData.roomWorldPoseOnHost = FlipZRotateY180(roomsData.alignmentData.Value.floorWorldPoseOnHost);
                    }

                    discoverResult = MRUKNativeFuncs.AnchorStoreStartQueryByLocalGroup(nativeSharedRoomsData, removeMissingRooms, sceneModel);
                }
            }
            else
            {
                discoverResult = MRUKNativeFuncs.AnchorStoreStartDiscovery(removeMissingRooms, sceneModel);
            }
            if (discoverResult != MRUKNativeFuncs.MrukResult.Success)
            {
                return ConvertResult(discoverResult);
            }

            var result = await WaitForDiscoveryFinished();

            if (result == LoadDeviceResult.NoRoomsFound)
            {
#if !UNITY_EDITOR && UNITY_ANDROID
                // If no rooms were loaded it could be due to missing scene permissions, check for this and print a warning
                // if that is the issue.
                if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                {
                    Debug.LogWarning($"MRUK couldn't load any scene data. The app does not have permissions for {OVRPermissionsRequester.ScenePermission}.");
                    return LoadDeviceResult.NoScenePermission;
                }
#elif UNITY_EDITOR
                if (OVRManager.isHmdPresent)
                {
                    // If in the editor and an HMD is present, assume either Link or XR Sim is being used.
                    Debug.LogWarning("MRUK couldn't load any scene data. Scene capture does not work over Link or XR Sim.\n" +
                                     "If using Link please capture a scene with the HMD in standalone mode, then access the scene model over Link.\n" +
                                     "If a scene model has already been captured, make sure the spatial data feature has been enabled in Meta Quest Link " +
                                     "(Settings > Beta > Spatial Data over Meta Quest Link).\n" +
                                     "If using XR Sim, make sure you have a synthetic environment loaded that has scene data associated with it.");
                }
#endif
                if (requestSceneCaptureIfNoDataFound && await OVRScene.RequestSpaceSetup())
                {
                    // Try again but this time don't request a space setup again if there are no rooms to avoid
                    // the user getting stuck in an infinite loop.
                    return await LoadSceneFromDeviceSharedLib(false, removeMissingRooms);
                }
            }

            if (result == LoadDeviceResult.Success)
            {
                InitializeScene();
            }

            return result;
        }

        private async Task<LoadDeviceResult> LoadSceneFromJsonSharedLib(string jsonString, bool removeMissingRooms = true)
        {
            if (_loadSceneTask != null)
            {
                return LoadDeviceResult.DiscoveryOngoing;
            }

            var loadJsonResult = MRUKNativeFuncs.AnchorStoreLoadSceneFromJson(jsonString, removeMissingRooms, MRUKNativeFuncs.MrukSceneModel.V2FallbackV1);
            if (loadJsonResult != MRUKNativeFuncs.MrukResult.Success)
            {
                return ConvertResult(loadJsonResult);
            }

            var result = await WaitForDiscoveryFinished();

            if (result == LoadDeviceResult.Success)
            {
                InitializeScene();
            }

            return result;
        }

        private unsafe string SaveSceneToJsonSharedLib(bool includeGlobalMesh, List<MRUKRoom> rooms)
        {
            Guid[] roomUuids = null;
            if (rooms != null)
            {
                roomUuids = new Guid[rooms.Count];
                for (int i = 0; i < rooms.Count; i++)
                {
                    roomUuids[i] = rooms[i].Anchor.Uuid;
                }
            }
            var rawString = MRUKNativeFuncs.AnchorStoreSaveSceneToJson(includeGlobalMesh, roomUuids, roomUuids != null ? (uint)roomUuids.Length : 0);
            string str = Marshal.PtrToStringUTF8((IntPtr)rawString);
            MRUKNativeFuncs.AnchorStoreFreeJson(rawString);
            return str;
        }

        private async Task<LoadDeviceResult> LoadSceneFromPrefabSharedLib(GameObject scenePrefab)
        {
            if (_loadSceneTask != null)
            {
                return LoadDeviceResult.DiscoveryOngoing;
            }

            List<GameObject> roomGameObjects = new List<GameObject>();
            // first, examine prefab to determine if it's a single room or collection of rooms
            // if the hierarchy is more than two levels deep, consider it a home
            if (scenePrefab.transform.childCount > 0 && scenePrefab.transform.GetChild(0).childCount > 0)
            {
                foreach (Transform room in scenePrefab.transform)
                {
                    roomGameObjects.Add(room.gameObject);
                }
            }
            else
            {
                roomGameObjects.Add(scenePrefab);
            }

            var roomAnchors = new List<MRUKNativeFuncs.MrukRoomAnchor>();
            var sceneAnchors = new List<MRUKNativeFuncs.MrukSceneAnchor>();
            var handles = new List<GCHandle>();
            MRUKNativeFuncs.MrukResult loadPrefabResult;
            try
            {
                foreach (var room in roomGameObjects)
                {
                    FindAllObjects(room, out var walls, out var volumes, out var planes);

                    var roomAnchor = new MRUKNativeFuncs.MrukRoomAnchor
                    {
                        uuid = Guid.NewGuid(),
                        pose = Pose.identity
                    };

                    // walls ordered sequentially, CW when viewed top-down
                    var orderedWalls = new List<MRUKNativeFuncs.MrukSceneAnchor>();

                    var unorderedWalls = new List<MRUKNativeFuncs.MrukSceneAnchor>();
                    var floorCorners = new List<Vector3>();

                    var wallHeight = 0.0f;

                    for (var i = 0; i < walls.Count; i++)
                    {
                        if (i == 0)
                        {
                            wallHeight = walls[i].transform.localScale.y;
                        }

                        var sceneAnchor = CreateMrukSceneAnchor(walls[i].name, handles, walls[i].transform.position, walls[i].transform.rotation, walls[i].transform.localScale,
                            AnchorRepresentation.PLANE);
                        sceneAnchor.roomUuid = roomAnchor.uuid;
                        sceneAnchor.pose.rotation *= Quaternion.AngleAxis(180, Vector3.up);

                        unorderedWalls.Add(sceneAnchor);
                    }

                    // There may be imprecision between the prefab walls (misaligned edges)
                    // so, we shift them so the edges perfectly match up:
                    // bottom left corner of wall is fixed, right corner matches left corner of wall to the right
                    var seedId = 0;
                    for (var i = 0; i < unorderedWalls.Count; i++)
                    {
                        var wall = GetAdjacentMrukSceneWall(ref seedId, unorderedWalls);
                        orderedWalls.Add(wall);

                        var wallRect = wall.plane;
                        var leftCorner = wall.pose.position + wall.pose.rotation * new Vector3(wallRect.x + wallRect.width, wallRect.y, 0.0f);
                        floorCorners.Add(leftCorner);
                    }

                    for (var i = 0; i < orderedWalls.Count; i++)
                    {
                        var planeRect = orderedWalls[i].plane;
                        var corner1 = floorCorners[i];
                        var nextID = (i == orderedWalls.Count - 1) ? 0 : i + 1;
                        var corner2 = floorCorners[nextID];

                        var wallRight = (corner1 - corner2);
                        wallRight.y = 0.0f;
                        var wallWidth = wallRight.magnitude;
                        wallRight /= wallWidth;
                        var wallUp = Vector3.up;
                        var wallFwd = Vector3.Cross(wallRight, wallUp);
                        var newPosition = (corner1 + corner2) * 0.5f + Vector3.up * (planeRect.height * 0.5f);
                        var newRotation = Quaternion.LookRotation(wallFwd, wallUp);
                        var newRect = new MRUKNativeFuncs.MrukPlane { x = -0.5f * wallWidth, y = planeRect.y, width = wallWidth, height = planeRect.height };

                        MRUKNativeFuncs.MrukSceneAnchor sceneAnchor = orderedWalls[i];
                        sceneAnchor.pose.position = newPosition;
                        sceneAnchor.pose.rotation = newRotation;
                        sceneAnchor.plane = newRect;
                        unsafe
                        {
                            sceneAnchor.planeBoundary[0] = new Vector2(newRect.x, newRect.y);
                            sceneAnchor.planeBoundary[1] = new Vector2(newRect.x + newRect.width, newRect.y);
                            sceneAnchor.planeBoundary[2] = new Vector2(newRect.x + newRect.width, newRect.y + newRect.height);
                            sceneAnchor.planeBoundary[3] = new Vector2(newRect.x, newRect.y + newRect.height);
                        }

                        orderedWalls[i] = sceneAnchor;
                        sceneAnchors.Add(sceneAnchor);
                    }
                    // Ensure the order when loading through the shared library is the same before
                    sceneAnchors.Reverse();

                    foreach (var volume in volumes)
                    {
                        var cubeScale = new Vector3(volume.transform.localScale.x, volume.transform.localScale.z, volume.transform.localScale.y);
                        var representation = AnchorRepresentation.VOLUME;
                        // Table, couch, bed and storage have a plane attached to them.
                        if (volume.transform.name == MRUKAnchor.SceneLabels.TABLE.ToString() ||
                            volume.transform.name == MRUKAnchor.SceneLabels.COUCH.ToString() ||
                            volume.transform.name == MRUKAnchor.SceneLabels.BED.ToString() ||
                            volume.transform.name == MRUKAnchor.SceneLabels.STORAGE.ToString())
                        {
                            representation |= AnchorRepresentation.PLANE;
                        }

                        var sceneAnchor = CreateMrukSceneAnchor(volume.name, handles, volume.transform.position, volume.transform.rotation, cubeScale, representation);
                        sceneAnchor.roomUuid = roomAnchor.uuid;

                        // in the prefab rooms, the cubes are more Unity-like and default: Y is up, pivot is centered
                        // this needs to be converted to Scene format, in which the pivot is on top of the cube and Z is up
                        // for couches we want the functional surface to be in the center
                        if (sceneAnchor.semanticLabel != MRUKNativeFuncs.MrukLabel.Couch)
                        {
                            sceneAnchor.pose.position += cubeScale.z * 0.5f * Vector3.up;
                        }

                        sceneAnchor.pose.rotation *= Quaternion.AngleAxis(-90, Vector3.right);
                        sceneAnchors.Add(sceneAnchor);
                    }

                    foreach (var plane in planes)
                    {
                        var sceneAnchor = CreateMrukSceneAnchor(plane.name, handles, plane.transform.position, plane.transform.rotation, plane.transform.localScale, AnchorRepresentation.PLANE);
                        sceneAnchor.roomUuid = roomAnchor.uuid;

                        // Unity quads have a surface normal facing the opposite direction
                        // Rather than have "backwards" walls in the room prefab, we just rotate them here
                        sceneAnchor.pose.rotation *= Quaternion.AngleAxis(180, Vector3.up);
                        sceneAnchors.Add(sceneAnchor);
                    }

                    // floor/ceiling anchor aligns with the longest wall, scaled to room size
                    MRUKNativeFuncs.MrukSceneAnchor longestWall = new();
                    float longestWidth = 0.0f;
                    foreach (var wall in orderedWalls)
                    {
                        float wallWidth = wall.plane.width;
                        if (wallWidth > longestWidth)
                        {
                            longestWidth = wallWidth;
                            longestWall = wall;
                        }
                    }

                    // calculate the room bounds, relative to the longest wall
                    float zMin = 0.0f;
                    float zMax = 0.0f;
                    float xMin = 0.0f;
                    float xMax = 0.0f;
                    {
                        var inverseRotation = Quaternion.Inverse(longestWall.pose.rotation);
                        for (int i = 0; i < floorCorners.Count; i++)
                        {
                            Vector3 localPos = inverseRotation * (floorCorners[i] - longestWall.pose.position);

                            zMin = i == 0 ? localPos.z : Mathf.Min(zMin, localPos.z);
                            zMax = i == 0 ? localPos.z : Mathf.Max(zMax, localPos.z);
                            xMin = i == 0 ? localPos.x : Mathf.Min(xMin, localPos.x);
                            xMax = i == 0 ? localPos.x : Mathf.Max(xMax, localPos.x);
                        }
                    }

                    Vector3 localRoomCenter = new Vector3((xMin + xMax) * 0.5f, 0, (zMin + zMax) * 0.5f);
                    Vector3 roomCenter = longestWall.pose.position + longestWall.pose.rotation * localRoomCenter;
                    roomCenter -= Vector3.up * wallHeight * 0.5f;
                    Vector3 floorScale = new Vector3(zMax - zMin, xMax - xMin, 1);

                    for (int i = 0; i < 2; i++)
                    {
                        string semanticLabel = (i == 0 ? "FLOOR" : "CEILING");

                        var position = roomCenter + Vector3.up * wallHeight * i;
                        float anchorFlip = i == 0 ? 1 : -1;
                        var rotation = Quaternion.LookRotation(longestWall.pose.up * anchorFlip, longestWall.pose.right);
                        var sceneAnchor = CreateMrukSceneAnchor(semanticLabel, handles, position, rotation, floorScale, AnchorRepresentation.PLANE);
                        sceneAnchor.roomUuid = roomAnchor.uuid;

                        var inverseRotation = Quaternion.Inverse(sceneAnchor.pose.rotation);
                        var boundary = new Vector2[floorCorners.Count];
                        var j = 0;
                        foreach (var corner in floorCorners)
                        {
                            var localCorner = inverseRotation * (corner - sceneAnchor.pose.position);
                            boundary[j++] = new Vector2(localCorner.x, localCorner.y);
                        }

                        if (i == 1)
                        {
                            Array.Reverse(boundary);
                        }

                        GCHandle handle = GCHandle.Alloc(boundary, GCHandleType.Pinned);
                        handles.Add(handle);
                        unsafe
                        {
                            sceneAnchor.planeBoundary = (Vector2*)handle.AddrOfPinnedObject();
                        }

                        sceneAnchor.planeBoundaryCount = (uint)boundary.Length;
                        sceneAnchor.hasPlane = true;

                        sceneAnchors.Add(sceneAnchor);
                    }

                    roomAnchors.Add(roomAnchor);
                }

                // Convert from Unity to OpenXR coordinate system
                for (int i = 0; i < sceneAnchors.Count; i++)
                {
                    var sceneAnchor = sceneAnchors[i];
                    sceneAnchor.pose = FlipZRotateY180(sceneAnchor.pose);
                    sceneAnchor.volume = ConvertVolume(sceneAnchor.volume);
                    sceneAnchor.plane = ConvertPlane(sceneAnchor.plane);
                    unsafe
                    {
                        int left = 0;
                        int right = (int)sceneAnchor.planeBoundaryCount - 1;
                        while (left < right)
                        {
                            // Reverse the order and flip the X coordinate to convert from OpenXR to Unity coordinate system
                            var posLeft = sceneAnchor.planeBoundary[left];
                            var posRight = sceneAnchor.planeBoundary[right];
                            sceneAnchor.planeBoundary[left] = FlipX(posRight);
                            sceneAnchor.planeBoundary[right] = FlipX(posLeft);
                            left++;
                            right--;
                        }

                        // Flip the middle position if it's an odd number
                        if (left == right)
                        {
                            sceneAnchor.planeBoundary[left] = FlipX(sceneAnchor.planeBoundary[left]);
                        }
                    }

                    sceneAnchors[i] = sceneAnchor;
                }

                unsafe
                {
                    fixed (MRUKNativeFuncs.MrukRoomAnchor* roomAnchorsPtr = roomAnchors.ToArray())
                    fixed (MRUKNativeFuncs.MrukSceneAnchor* sceneAnchorsPtr = sceneAnchors.ToArray())
                    {
                        loadPrefabResult = MRUKNativeFuncs.AnchorStoreLoadSceneFromPrefab(roomAnchorsPtr, (uint)roomAnchors.Count, sceneAnchorsPtr, (uint)sceneAnchors.Count);
                    }

                }
            }
            finally
            {
                foreach (var handle in handles)
                {
                    handle.Free();
                }
            }

            if (loadPrefabResult != MRUKNativeFuncs.MrukResult.Success)
            {
                return ConvertResult(loadPrefabResult);
            }

            var result = await WaitForDiscoveryFinished();

            if (result == LoadDeviceResult.Success)
            {
                InitializeScene();
            }

            return result;
        }

        private MRUKNativeFuncs.MrukSceneAnchor GetAdjacentMrukSceneWall(ref int thisID, List<MRUKNativeFuncs.MrukSceneAnchor> randomWalls)
        {
            Vector2 thisWallScale = new Vector2(randomWalls[thisID].plane.width, randomWalls[thisID].plane.height);

            Vector3 halfScale = thisWallScale * 0.5f;
            Vector3 bottomRight = randomWalls[thisID].pose.position - randomWalls[thisID].pose.up * halfScale.y - randomWalls[thisID].pose.right * halfScale.x;
            float closestCornerDistance = Mathf.Infinity;
            // When searching for a matching corner, the correct one should match positions. If they don't, assume there's a crack in the room.
            // This should be an impossible scenario and likely means broken data from Room Setup.
            int rightWallID = 0;
            for (int i = 0; i < randomWalls.Count; i++)
            {
                // compare to bottom left point of other walls
                if (i != thisID)
                {
                    Vector2 testWallHalfScale = new Vector2(randomWalls[i].plane.width * 0.5f, randomWalls[i].plane.height * 0.5f);
                    Vector3 bottomLeft = randomWalls[i].pose.position - randomWalls[i].pose.up * testWallHalfScale.y + randomWalls[i].pose.right * testWallHalfScale.x;
                    float thisCornerDistance = Vector3.Distance(bottomLeft, bottomRight);
                    if (thisCornerDistance < closestCornerDistance)
                    {
                        closestCornerDistance = thisCornerDistance;
                        rightWallID = i;
                    }
                }
            }

            thisID = rightWallID;
            return randomWalls[thisID];
        }

        private unsafe MRUKNativeFuncs.MrukSceneAnchor CreateMrukSceneAnchor(string semanticLabel, List<GCHandle> handles, Vector3 position, Quaternion rotation, Vector3 objScale, AnchorRepresentation representation)
        {
            MRUKNativeFuncs.MrukSceneAnchor sceneAnchor = new MRUKNativeFuncs.MrukSceneAnchor
            {
                semanticLabel = (MRUKNativeFuncs.MrukLabel)(1 << (int)OVRSemanticLabels.FromApiLabel(semanticLabel))
            };
            sceneAnchor.pose.position = position;
            sceneAnchor.pose.rotation = rotation;
            if ((representation & AnchorRepresentation.PLANE) != 0)
            {
                var rect = new MRUKNativeFuncs.MrukPlane { x = -0.5f * objScale.x, y = -0.5f * objScale.y, width = objScale.x, height = objScale.y };
                sceneAnchor.plane = rect;
                var boundary = new Vector2[]
                {
                    new(rect.x, rect.y),
                    new(rect.x + rect.width, rect.y),
                    new(rect.x + rect.width, rect.y + rect.height),
                    new(rect.x, rect.y + rect.height),
                };
                GCHandle handle = GCHandle.Alloc(boundary, GCHandleType.Pinned);
                handles.Add(handle);
                sceneAnchor.planeBoundary = (Vector2*)handle.AddrOfPinnedObject();
                sceneAnchor.planeBoundaryCount = (uint)boundary.Length;
                sceneAnchor.hasPlane = true;
            }

            if ((representation & AnchorRepresentation.VOLUME) != 0)
            {
                Vector3 offsetCenter = new Vector3(0, 0, -objScale.z * 0.5f);
                // for couches we want the functional surface to be in the center
                if (sceneAnchor.semanticLabel == MRUKNativeFuncs.MrukLabel.Couch)
                {
                    offsetCenter = Vector3.zero;
                }

                sceneAnchor.volume = new MRUKNativeFuncs.MrukVolume { min = offsetCenter - 0.5f * objScale, max = offsetCenter + 0.5f * objScale };
                sceneAnchor.hasVolume = true;
            }

            sceneAnchor.uuid = Guid.NewGuid();
            return sceneAnchor;
        }

        private void ClearSceneSharedLib()
        {
            MRUKNativeFuncs.AnchorStoreClearRooms();
        }

        private static Vector2 FlipX(Vector2 vector)
        {
            return new Vector2(-vector.x, vector.y);
        }

        private static Vector3 FlipX(Vector3 vector)
        {
            return new Vector3(-vector.x, vector.y, vector.z);
        }

        private static Vector3 FlipZ(Vector3 vector)
        {
            return new Vector3(vector.x, vector.y, -vector.z);
        }

        private static Quaternion FlipZ(Quaternion quaternion)
        {
            return new Quaternion(-quaternion.x, -quaternion.y, quaternion.z, quaternion.w);
        }

        private static Quaternion FlipZRotateY180(Quaternion rotation)
        {
            return new Quaternion(-rotation.z, rotation.w, -rotation.x, rotation.y);
        }

        private static Pose FlipZ(Pose pose)
        {
            // Transform from OpenXR Right-handed coordinate system
            // to Unity Left-handed coordinate system
            return new Pose(FlipZ(pose.position), FlipZ(pose.rotation));
        }

        internal static Pose FlipZRotateY180(Pose pose)
        {
            // Transform from OpenXR Right-handed coordinate system
            // to Unity Left-handed coordinate system with additional 180 rotation around +y
            return new Pose(FlipZ(pose.position), FlipZRotateY180(pose.rotation));
        }

        private static MRUKNativeFuncs.MrukVolume ConvertVolume(MRUKNativeFuncs.MrukVolume volume)
        {
            // We need to flip the z axis to convert from OpenXR to Unity
            // And then, because of the z-axis positive normal, rotate 180 around +y
            // This ends up being equivalent to flipping x axis
            var min = volume.min;
            var max = volume.max;
            return new MRUKNativeFuncs.MrukVolume { min = new Vector3(-max.x, min.y, min.z), max = new Vector3(-min.x, max.y, max.z) };
        }

        private static MRUKNativeFuncs.MrukPlane ConvertPlane(MRUKNativeFuncs.MrukPlane plane)
        {
            // Flip X to convert from OpenXR to Unity coordinate system
            return new MRUKNativeFuncs.MrukPlane { x = -(plane.x + plane.width), y = plane.y, width = plane.width, height = plane.height };
        }

        private MRUKRoom FindRoomByUuid(Guid uuid)
        {
            foreach (var room in Rooms)
            {
                if (room.Anchor.Uuid == uuid)
                {
                    return room;
                }
            }

            return null;
        }

        private static unsafe void UpdateAnchorProperties(MRUKAnchor anchor, ref MRUKNativeFuncs.MrukSceneAnchor sceneAnchor)
        {
            MRUKAnchor.SceneLabels labels = ConvertLabel(sceneAnchor.semanticLabel);
            var name = labels != 0 ? labels.ToString() : "UNDEFINED_ANCHOR";

            anchor.gameObject.name = name;

            var convertedPos = FlipZRotateY180(sceneAnchor.pose);
            anchor.InitialPose = convertedPos;
            anchor.gameObject.transform.SetPositionAndRotation(convertedPos.position, convertedPos.rotation);

            anchor.Label = labels;
            anchor.Anchor = new OVRAnchor(sceneAnchor.space, sceneAnchor.uuid);
            if (sceneAnchor.hasPlane)
            {
                anchor.PlaneBoundary2D = new List<Vector2>((int)sceneAnchor.planeBoundaryCount);
                for (int i = 0; i < sceneAnchor.planeBoundaryCount; ++i)
                {
                    // Reverse the order and flip the X coordinate to convert from OpenXR to Unity coordinate system
                    var pos = sceneAnchor.planeBoundary[sceneAnchor.planeBoundaryCount - i - 1];
                    anchor.PlaneBoundary2D.Add(FlipX(pos));
                }

                // Flip X to convert from OpenXR to Unity coordinate system
                var plane = ConvertPlane(sceneAnchor.plane);
                anchor.PlaneRect = new Rect(plane.x, plane.y, plane.width, plane.height);
            }
            else
            {
                anchor.PlaneBoundary2D = null;
                anchor.PlaneRect = null;
            }

            if (sceneAnchor.hasVolume)
            {
                var v = ConvertVolume(sceneAnchor.volume);
                anchor.VolumeBounds = new Bounds((v.min + v.max) / 2, v.max - v.min);
            }
            else
            {
                anchor.VolumeBounds = null;
            }

            if (sceneAnchor.globalMeshPositionsCount > 0 && sceneAnchor.globalMeshIndicesCount > 0)
            {
                var vertices = new Vector3[sceneAnchor.globalMeshPositionsCount];
                for (int i = 0; i < sceneAnchor.globalMeshPositionsCount; ++i)
                {
                    // Flip the X axis to convert from OpenXR to Unity coordinate system
                    vertices[i] = FlipX(sceneAnchor.globalMeshPositions[i]);
                }

                var triangles = new int[sceneAnchor.globalMeshIndicesCount];
                Assert.IsTrue(sceneAnchor.globalMeshIndicesCount % 3 == 0, "Number of indices must be a multiple of 3");
                for (int i = 0; i < sceneAnchor.globalMeshIndicesCount; i += 3)
                {
                    // Change the winding order to account for OpenXR to Unity coordinate system
                    triangles[i + 0] = (int)sceneAnchor.globalMeshIndices[i + 0];
                    triangles[i + 1] = (int)sceneAnchor.globalMeshIndices[i + 2];
                    triangles[i + 2] = (int)sceneAnchor.globalMeshIndices[i + 1];
                }

                var newMesh = new Mesh
                {
                    indexFormat = sceneAnchor.globalMeshIndicesCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                    vertices = vertices,
                    triangles = triangles
                };
                anchor.Mesh = newMesh;
            }
            else
            {
                anchor.Mesh = null;
            }
        }

        [MonoPInvokeCallback(typeof(OVRPlugin.OpenXREventDelegateType))]
        private static void OnOpenXrEvent(IntPtr data, IntPtr context)
        {
            try
            {
                MRUKNativeFuncs.AnchorStoreOnOpenXrEvent(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnPreRoomAnchorAdded))]
        private static void OnPreRoomAnchorAdded(ref MRUKNativeFuncs.MrukRoomAnchor roomAnchor, IntPtr userContext)
        {
            try
            {
                var uuid = roomAnchor.uuid;
                GameObject sceneRoom = new GameObject($"Room - {uuid}");
                MRUKRoom room = sceneRoom.AddComponent<MRUKRoom>();
                room.Anchor = new OVRAnchor(roomAnchor.space, uuid);
                if (roomAnchor.pose != Pose.identity)
                {
                    var convertedPose = FlipZRotateY180(roomAnchor.pose);
                    room.InitialPose = convertedPose;
                    room.transform.SetPositionAndRotation(convertedPose.position, convertedPose.rotation);
                }
                Instance.Rooms.Add(room);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnRoomAnchorAdded))]
        private static void OnRoomAnchorAdded(ref MRUKNativeFuncs.MrukRoomAnchor roomAnchor, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(roomAnchor.uuid);
                Debug.Assert(room != null, "Room should have been added in OnPreRoomAnchorAdded");
                room.ComputeRoomInfo();
                Instance.RoomCreatedEvent.Invoke(room);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnRoomAnchorUpdated))]
        private static void OnRoomAnchorUpdated(ref MRUKNativeFuncs.MrukRoomAnchor roomAnchor, ref Guid oldRoomAnchorUuid, bool significantChange, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(oldRoomAnchorUuid);
                Debug.Assert(room != null, "Room should exist if it is being updated");
                room.Anchor = new OVRAnchor(roomAnchor.space, roomAnchor.uuid);
                if (significantChange)
                {
                    room.ComputeRoomInfo();
                    Instance.RoomUpdatedEvent.Invoke(room);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnRoomAnchorRemoved))]
        private static void OnRoomAnchorRemoved(ref MRUKNativeFuncs.MrukRoomAnchor roomAnchor, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(roomAnchor.uuid);
                Debug.Assert(room != null, "Room should exist if it is being removed");
                Instance.RoomRemovedEvent.Invoke(room);
                Instance.Rooms.Remove(room);
                Utilities.DestroyGameObjectAndChildren(room.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnSceneAnchorAdded))]
        private static void OnSceneAnchorAdded(ref MRUKNativeFuncs.MrukSceneAnchor sceneAnchor, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(sceneAnchor.roomUuid);
                Debug.Assert(room != null, "Room should exist");

                var anchorGo = new GameObject();
                var anchor = anchorGo.AddComponent<MRUKAnchor>();
                anchor.Room = room;
                anchorGo.transform.SetParent(room.transform);

                UpdateAnchorProperties(anchor, ref sceneAnchor);

                room.Anchors.Add(anchor);

                if ((anchor.Label &
                     (MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE)) != 0)
                {
                    room.WallAnchors.Add(anchor);
                }

                if ((anchor.Label & MRUKAnchor.SceneLabels.CEILING) != 0)
                {
                    room.CeilingAnchor = anchor;
                }

                if ((anchor.Label & MRUKAnchor.SceneLabels.FLOOR) != 0)
                {
                    room.FloorAnchor = anchor;
                }

                room.AnchorCreatedEvent.Invoke(anchor);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnSceneAnchorUpdated))]
        private static void OnSceneAnchorUpdated(ref MRUKNativeFuncs.MrukSceneAnchor sceneAnchor, bool significantChange, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(sceneAnchor.roomUuid);
                Debug.Assert(room != null, "Room should exist");
                MRUKAnchor anchor = room.FindAnchorByUuid(sceneAnchor.uuid);
                Debug.Assert(anchor != null, "Anchor should exist");

                UpdateAnchorProperties(anchor, ref sceneAnchor);

                if (significantChange)
                {
                    room.AnchorUpdatedEvent.Invoke(anchor);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnSceneAnchorRemoved))]
        private static void OnSceneAnchorRemoved(ref MRUKNativeFuncs.MrukSceneAnchor sceneAnchor, IntPtr userContext)
        {
            try
            {
                MRUKRoom room = Instance.FindRoomByUuid(sceneAnchor.roomUuid);
                Debug.Assert(room != null, "Room should exist");
                MRUKAnchor anchor = room.FindAnchorByUuid(sceneAnchor.uuid);
                Debug.Assert(anchor != null, "Anchor should exist");

                room.AnchorRemovedEvent.Invoke(anchor);
                room.RemoveAndDestroyAnchor(anchor);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnDiscoveryFinished))]
        private static void OnDiscoveryFinished(MRUKNativeFuncs.MrukResult result, IntPtr userContext)
        {
            try
            {
                Instance._loadSceneTask?.SetResult(ConvertResult(result));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.MrukOnEnvironmentRaycasterCreated))]
        private static void OnEnvironmentRaycasterCreated(MRUKNativeFuncs.MrukResult result, IntPtr userContext)
        {
        }

        private async Task<LoadDeviceResult> WaitForDiscoveryFinished()
        {
            _loadSceneTask = OVRTask.Create<LoadDeviceResult>(Guid.NewGuid());
            var result = await _loadSceneTask.Value;
            _loadSceneTask = null;
            return result;
        }

        private static LoadDeviceResult ConvertResult(MRUKNativeFuncs.MrukResult result)
        {
            switch (result)
            {
                case MRUKNativeFuncs.MrukResult.Success:
                    return LoadDeviceResult.Success;
                case MRUKNativeFuncs.MrukResult.ErrorDiscoveryOngoing:
                    return LoadDeviceResult.DiscoveryOngoing;
                case MRUKNativeFuncs.MrukResult.ErrorInvalidJson:
                    return LoadDeviceResult.FailureDataIsInvalid;
                case MRUKNativeFuncs.MrukResult.ErrorNoRoomsFound:
                    return LoadDeviceResult.NoRoomsFound;
                case MRUKNativeFuncs.MrukResult.ErrorInsufficientResources:
                    return LoadDeviceResult.FailureInsufficientResources;
                case MRUKNativeFuncs.MrukResult.ErrorStorageAtCapacity:
                    return LoadDeviceResult.StorageAtCapacity;
                case MRUKNativeFuncs.MrukResult.ErrorInsufficientView:
                    return LoadDeviceResult.FailureInsufficientView;
                case MRUKNativeFuncs.MrukResult.ErrorPermissionInsufficient:
                    return LoadDeviceResult.FailurePermissionInsufficient;
                case MRUKNativeFuncs.MrukResult.ErrorRateLimited:
                    return LoadDeviceResult.FailureRateLimited;
                case MRUKNativeFuncs.MrukResult.ErrorTooDark:
                    return LoadDeviceResult.FailureTooDark;
                case MRUKNativeFuncs.MrukResult.ErrorTooBright:
                    return LoadDeviceResult.FailureTooBright;
                default:
                    return LoadDeviceResult.Failure;
            }
        }

        private static MRUKAnchor.SceneLabels ConvertLabel(MRUKNativeFuncs.MrukLabel label)
        {
            return (MRUKAnchor.SceneLabels)label;
        }

    }
}

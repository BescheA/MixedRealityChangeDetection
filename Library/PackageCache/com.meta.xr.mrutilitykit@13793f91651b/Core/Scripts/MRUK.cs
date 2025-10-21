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
using System.Threading.Tasks;
using AOT;
using Meta.XR.Util;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Assertions;
using UnityEngine.Diagnostics;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Meta.XR.ImmersiveDebugger;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    /// The <c>MRUK</c> class provides a comprehensive suite of utility functions designed work with scene data and to
    /// facilitate the management and querying of scenes within the Unity editor.
    /// </summary>
    /// <remarks>
    /// This class is integral for developers working within the MR utility kit framework, offering capabilities such as to load scenes, to register callbacks for scene events,
    /// to manage room and anchor data, and handle world locking to ensure virtual objects remain synchronized with the real world.
    /// Use the <see cref="SceneDataSource"/> enum to specify positioning strategies and data source, (such as device data, prefabs or json files).
    /// Only one instance of this class can exist in a scene. When using world locking by setting <see cref="EnableWorldLock"/> to true,
    /// make sure to use the <see cref="OVRCameraRig"/> component in your scene, as it is used to determine the camera's position and orientation.
    /// </remarks>
    [HelpURL("https://developers.meta.com/horizon/reference/mruk/latest/class_meta_x_r_m_r_utility_kit_m_r_u_k")]
    [Feature(Feature.Scene)]
    public partial class MRUK : MonoBehaviour
    {
        /// <summary>
        /// when interacting specifically with tops of volumes, this can be used to
        /// specify where the return position should be aligned on the surface
        /// e.g. some apps  might want a position right in the center of the table (chess)
        /// for others, the edge may be more important (piano or pong)
        /// </summary>
        public enum PositioningMethod
        {
            DEFAULT,
            CENTER,
            EDGE
        }

        /// <summary>
        ///     Specify the source of the scene data.
        /// </summary>
        public enum SceneDataSource
        {
            /// <summary>
            ///     Load scene data from the device.
            /// </summary>
            Device,

            /// <summary>
            ///     Load scene data from prefabs.
            /// </summary>
            Prefab,

            /// <summary>
            ///     First try to load data from the device and if none can be found
            ///     fall back to loading from a prefab.
            /// </summary>
            DeviceWithPrefabFallback,

            /// <summary>
            ///     Load Scene from a Json file
            /// </summary>
            Json,

            /// <summary>
            ///     First try to load data from the device and if none can be found
            ///     fall back to loading from a Json file
            /// </summary>
            DeviceWithJsonFallback,
        }

        /// <summary>
        ///     Specifies the filtering options for selecting rooms within the scene data.
        /// </summary>
        public enum RoomFilter
        {
            None,
            CurrentRoomOnly,
            AllRooms,
        };

        /// <summary>
        ///     Return value from the call to LoadSceneFromDevice.
        ///     This is used to indicate if the scene was loaded successfully or if there was an error.
        /// </summary>
        public enum LoadDeviceResult
        {
            /// <summary>
            ///     Scene data loaded successfully.
            /// </summary>
            Success = OVRAnchor.FetchResult.Success,

            /// <summary>
            ///     User did not grant scene permissions.
            /// </summary>
            NoScenePermission = 1,

            /// <summary>
            ///     No rooms were found (e.g. User did not go through space setup)
            /// </summary>
            NoRoomsFound = 2,

            /// <summary>
            ///     Only one discovery can happen at a time
            /// </summary>
            DiscoveryOngoing = 3,

            /// <summary>
            ///     Generic failure
            /// </summary>
            Failure = OVRPlugin.Result.Failure,

            /// <summary>
            ///     Storage at capacity
            /// </summary>
            StorageAtCapacity = OVRPlugin.Result.Failure_SpaceStorageAtCapacity,

            /// <summary>
            ///     Not initialized (this can happen when running in the editor without Link)
            /// </summary>
            NotInitialized = OVRPlugin.Result.Failure_NotInitialized,

            /// <summary>
            ///     Invalid data.
            /// </summary>
            FailureDataIsInvalid = OVRAnchor.FetchResult.FailureDataIsInvalid,

            /// <summary>
            ///     Resource limitation prevented this operation from executing.
            /// </summary>
            /// <remarks>
            ///     Recommend retrying, perhaps after a short delay and/or reducing memory consumption.
            /// </remarks>
            FailureInsufficientResources = OVRAnchor.FetchResult.FailureInsufficientResources,

            /// <summary>
            ///     Insufficient view.
            /// </summary>
            /// <remarks>
            ///     The user needs to look around the environment more for anchor tracking to function.
            /// </remarks>
            FailureInsufficientView = OVRAnchor.FetchResult.FailureInsufficientView,

            /// <summary>
            ///     Insufficient permission.
            /// </summary>
            /// <remarks>
            ///     Recommend confirming the status of the required permissions needed for using anchor APIs.
            /// </remarks>
            FailurePermissionInsufficient = OVRAnchor.FetchResult.FailurePermissionInsufficient,

            /// <summary>
            ///     Operation canceled due to rate limiting.
            /// </summary>
            /// <remarks>
            ///     Recommend retrying after a short delay.
            /// </remarks>
            FailureRateLimited = OVRAnchor.FetchResult.FailureRateLimited,

            /// <summary>
            ///     Too dark.
            /// </summary>
            /// <remarks>
            ///     The environment is too dark to load the anchor.
            /// </remarks>
            FailureTooDark = OVRAnchor.FetchResult.FailureTooDark,

            /// <summary>
            ///     Too bright.
            /// </summary>
            /// <remarks>
            ///     The environment is too bright to load the anchor.
            /// </remarks>
            FailureTooBright = OVRAnchor.FetchResult.FailureTooBright,
        };

        /// <summary>
        /// This struct is used to manage which rooms and anchors are not being tracked.
        /// </summary>
        internal struct SceneTrackingSettings
        {
            internal HashSet<MRUKRoom> UnTrackedRooms;
            internal HashSet<MRUKAnchor> UnTrackedAnchors;
        }

        /// <summary>
        ///  Defines flags for different types of surfaces that can be identified or used within a scene.
        ///  This is mainly used when querying for specifin anchors' surfaces, finding random positions, or  ray casting.
        /// </summary>
        [Flags]
        public enum SurfaceType
        {
            FACING_UP = 1 << 0,
            FACING_DOWN = 1 << 1,
            VERTICAL = 1 << 2,
        };

        [Flags]
        private enum AnchorRepresentation
        {
            PLANE = 1 << 0,
            VOLUME = 1 << 1,
        }

        /// <summary>
        ///  Gets a value indicating whether the component has been initialized.
        ///  When subscribing a callback to the <see cref="SceneLoadedEvent"/> event,
        ///  it will be triggered right away if the component is already initialized.
        /// </summary>
        public bool IsInitialized
        {
            get;
            private set;
        } = false;

        /// <summary>
        ///     Event that is triggered when the scene is loaded.
        ///     When subscribing a callback to this event,
        ///     it will be triggered right away if the <see cref="MRUK"/> component is already initialized.
        /// </summary>
        [field: SerializeField, FormerlySerializedAs(nameof(SceneLoadedEvent))]
        public UnityEvent SceneLoadedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        ///     This event is triggered when a new room is created from scene capture.
        ///     Useful for apps to intercept the room creation and do some custom logic
        ///     with the new room.
        /// </summary>
        [field: SerializeField, FormerlySerializedAs(nameof(RoomCreatedEvent))]
        public UnityEvent<MRUKRoom> RoomCreatedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        ///     Event that is triggered when a room is updated,
        ///     this can happen when the user adds or removes an anchor from the room.
        ///     Useful for apps to intercept the room update and do some custom logic
        ///     with the anchors in the room.
        /// </summary>
        [field: SerializeField, FormerlySerializedAs(nameof(RoomUpdatedEvent))]
        public UnityEvent<MRUKRoom> RoomUpdatedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        ///     Event that is triggered when a room is removed.
        ///     Useful for apps to intercept the room removal and do some custom logic
        ///     with the room just removed.
        /// </summary>
        [field: SerializeField, FormerlySerializedAs(nameof(RoomRemovedEvent))]
        public UnityEvent<MRUKRoom> RoomRemovedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        ///     When world locking is enabled the position and rotation of the OVRCameraRig/TrackingSpace transform will be adjusted each frame to ensure
        ///     the room anchors are where they should be relative to the camera position. This is necessary to
        ///     ensure the position of the virtual objects in the world do not get out of sync with the real world.
        ///     Make sure that there your scene is making use of the <see cref="OVRCameraRig"/> component.
        /// </summary>
        public bool EnableWorldLock = true;

        /// <summary>
        ///     This property indicates if world lock is currently active. This means that <see cref="EnableWorldLock"/> is enabled and the current
        ///     room has successfully been localized. In some cases such as when the headset goes into standby, the user moves to another room and
        ///     comes back out of standby the headset may fail to localize and this property will be false. The property may become true again once
        ///     the device is able to localise the room again (e.g. if the user walks back into the original room). This property can be used to show
        ///     a UI panel asking the user to return to their room.
        /// </summary>
        public bool IsWorldLockActive => EnableWorldLock && _worldLockActive;

        /// <summary>
        ///     When the <see cref="EnableWorldLock"/> is enabled, MRUK will modify the TrackingSpace transform, overwriting any manual changes.
        ///     Use this field to change the position and rotation of the TrackingSpace transform when the world locking is enabled.
        /// </summary>
        [HideInInspector]
        public Matrix4x4 TrackingSpaceOffset = Matrix4x4.identity;

        internal OVRCameraRig _cameraRig { get; private set; }
        private bool _worldLockActive = false;
        private bool _worldLockWasEnabled = false;
        private bool _loadSceneCalled = false;
        private Pose? _prevTrackingSpacePose = default;
        private readonly List<OVRSemanticLabels.Classification> _classificationsBuffer = new List<OVRSemanticLabels.Classification>(1);

        /// <summary>
        ///     This is the final event that tells developer code that Scene API and MR Utility Kit have been initialized, and that the room can be queried.
        /// </summary>
        void InitializeScene()
        {
            try
            {
                SceneLoadedEvent.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            IsInitialized = true;
        }

        /// <summary>
        ///     Register to receive a callback when the scene is loaded. If the scene is already loaded
        ///     at the time this is called, the callback will be invoked immediatly.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterSceneLoadedCallback(UnityAction callback)
        {
            SceneLoadedEvent.AddListener(callback);
            if (IsInitialized)
            {
                callback();
            }
        }

        /// <summary>
        ///     Register to receive a callback when a new room has been created from scene capture.
        /// </summary>
        /// <param name="callback">
        ///     - `MRUKRoom` The created room object.
        /// </param>
        [Obsolete("Use UnityEvent RoomCreatedEvent directly instead")]
        public void RegisterRoomCreatedCallback(UnityAction<MRUKRoom> callback)
        {
            RoomCreatedEvent.AddListener(callback);
        }

        /// <summary>
        ///     Register to receive a callback when a room has been updated from scene capture.
        /// </summary>
        /// <param name="callback">
        ///     - `MRUKRoom` The updated room object.
        /// </param>
        [Obsolete("Use UnityEvent RoomUpdatedEvent directly instead")]
        public void RegisterRoomUpdatedCallback(UnityAction<MRUKRoom> callback)
        {
            RoomUpdatedEvent.AddListener(callback);
        }

        /// <summary>
        ///     Registers a callback function to be called before the room is removed.
        /// </summary>
        /// <param name="callback">
        ///     The function to be called when the room is removed. It takes one parameter:
        ///     - `MRUKRoom` The removed room object.
        /// </param>
        [Obsolete("Use UnityEvent RoomRemovedEvent directly instead")]
        public void RegisterRoomRemovedCallback(UnityAction<MRUKRoom> callback)
        {
            RoomRemovedEvent.AddListener(callback);
        }

        /// <summary>
        /// Get a list of all the rooms in the scene.
        /// </summary>
        /// <returns>A list of MRUKRoom objects representing all the rooms in the scene.</returns>
        [Obsolete("Use Rooms property instead")]
        public List<MRUKRoom> GetRooms() => Rooms;

        /// <summary>
        /// Get a flat list of all Anchors in the scene
        /// </summary>
        /// <returns>A list of MRUKAnchor objects representing all the anchors in the current room.</returns>
        [Obsolete("Use GetCurrentRoom().Anchors instead")]
        public List<MRUKAnchor> GetAnchors() => GetCurrentRoom().Anchors;

        /// <summary>
        ///  Returns the current room the headset is in. If the headset is not in any given room
        ///  then it will return the room the headset was last in when this function was called.
        ///  If the headset hasn't been in a valid room yet then return the first room in the list.
        ///  If no rooms have been loaded yet then return null.
        /// </summary>
        /// <returns>The current <see cref="MRUKRoom"/> based on the headset's position, or null if no rooms are available.</returns>
        public MRUKRoom GetCurrentRoom()
        {
            // This is a rather expensive operation, we should only do it at most once per frame.
            if (_cachedCurrentRoomFrame != Time.frameCount)
            {
                if (_cameraRig?.centerEyeAnchor.position is Vector3 eyePos)
                {
                    MRUKRoom currentRoom = null;
                    foreach (var room in Rooms)
                    {
                        if (room.IsPositionInRoom(eyePos, false))
                        {
                            currentRoom = room;
                            // In some cases the user may be in multiple rooms at once. If this happens
                            // then we give precedence to rooms which have been loaded locally
                            if (room.IsLocal)
                            {
                                break;
                            }
                        }
                    }
                    if (currentRoom != null)
                    {
                        _cachedCurrentRoom = currentRoom;
                        _cachedCurrentRoomFrame = Time.frameCount;
                        return currentRoom;
                    }
                }
            }

            if (_cachedCurrentRoom != null)
            {
                return _cachedCurrentRoom;
            }

            if (Rooms.Count > 0)
            {
                return Rooms[0];
            }

            return null;
        }

        /// <summary>
        ///     Checks whether any anchors can be loaded.
        /// </summary>
        /// <returns>
        ///     Returns a task-based bool, which is true if
        ///     there are any scene anchors in the system, and false
        ///     otherwise. If false is returned, then either
        ///     the scene permission needs to be set, or the user
        ///     has to run Scene Capture.
        /// </returns>
        public static async Task<bool> HasSceneModel()
        {
            var rooms = new List<OVRAnchor>();
            var result = await OVRAnchor.FetchAnchorsAsync(rooms, new OVRAnchor.FetchOptions
            {
                SingleComponentType = typeof(OVRRoomLayout)
            });
            return result.Success && rooms.Count > 0;
        }

        /// <summary>
        /// Represents the settings for the <see cref"MRUK"/> instance,
        /// including data source configurations, startup behaviors, and other scene related settings.
        /// These settings are serialized and can be modified in the Unity Editor from the <see cref"MRUK"/> component.
        /// </summary>
        [Serializable]
        public partial class MRUKSettings
        {
            [Header("Data Source settings")]
            [SerializeField, Tooltip("Where to load the data from.")]
            /// <summary>
            /// Where to load the data from.
            /// </summary>
            public SceneDataSource DataSource = SceneDataSource.Device;

            /// <summary>
            /// The index (0-based) into the RoomPrefabs or SceneJsons array
            /// determining which room to load. -1 is random.
            /// </summary>
            [SerializeField, Tooltip("The index (0-based) into the RoomPrefabs or SceneJsons array; -1 is random.")]
            public int RoomIndex = -1;

            /// <summary>
            /// The list of prefab rooms to use.
            /// when <see cref"SceneDataSource"/> is set to Prefab or DeviceWithPrefabFallback.
            /// The MR Utilty kit package includes a few sample rooms to use as a starting point for your own scene.
            /// These rooms can be modified in the Unity editor and then saved as a new prefab.
            /// </summary>
            [SerializeField, Tooltip("The list of prefab rooms to use.")]
            public GameObject[] RoomPrefabs;

            /// <summary>
            /// The list of JSON text files with scene data to use when
            /// <see cref"SceneDataSource"/> is set to JSON or DeviceWithJSONFallback.
            /// The MR Utilty kit package includes a few serialized sample rooms to use as a starting point for your own scene.
            /// </summary>
            [SerializeField, Tooltip("The list of JSON text files with scene data to use. Uses RoomIndex")]
            public TextAsset[] SceneJsons;

            /// <summary>
            /// Trigger a scene load on startup. If set to false, you can call LoadSceneFromDevice(), LoadScene
            /// or LoadSceneFromJsonString() manually.
            /// </summary>
            [Space]
            [Header("Startup settings")]
            [SerializeField, Tooltip("Trigger a scene load on startup. If set to false, you can call LoadSceneFromDevice(), LoadSceneFromPrefab() or LoadSceneFromJsonString() manually.")]
            public bool LoadSceneOnStartup = true;

            [Space]
            [Header("Other settings")]
            [SerializeField, Tooltip("The width of a seat. Used to calculate seat positions with the COUCH label.")]
            /// <summary>
            /// The width of a seat. Used to calculate seat positions with the COUCH label. Default is 0.6f.
            /// </summary>
            public float SeatWidth = 0.6f;


            // SceneJson has been replaced with `TextAsset[] SceneJsons` defined above
            [SerializeField, HideInInspector, Obsolete]
            internal string SceneJson;
        }

        [Tooltip("Contains all the information regarding data loading.")]
        /// <summary>
        /// Contains all the information regarding data loading.
        /// </summary>
        public MRUKSettings SceneSettings;

        MRUKRoom _cachedCurrentRoom = null;
        int _cachedCurrentRoomFrame = 0;


        /// <summary>
        ///     List of all the rooms in the scene.
        /// </summary>
        public List<MRUKRoom> Rooms
        {
            get;
        } = new();


        /// <summary>
        /// Gets the singleton instance of the MRUK class.
        /// </summary>
        public static MRUK Instance
        {
            get;
            private set;
        }

        [SerializeField] internal GameObject _immersiveSceneDebuggerPrefab;

        void Awake()
        {
            _cameraRig = FindAnyObjectByType<OVRCameraRig>();

            if (Instance != null && Instance != this)
            {
                Debug.Assert(false, "There should be only one instance of MRUK!");
                Destroy(this);
            }
            else
            {
                Instance = this;
            }

            MRUKNative.LoadMRUKSharedLibrary();
            unsafe
            {
                MRUKNativeFuncs.SetLogPrinter(OnSharedLibLog);
            }

            InitializeAnchorStore();

            if (SceneSettings != null && SceneSettings.LoadSceneOnStartup)
            {
                // We can't await for the LoadScene result because Awake is not async, silence the warning
#pragma warning disable CS4014
#if !UNITY_EDITOR && UNITY_ANDROID
                // If we are going to load from device we need to ensure we have permissions first
                if ((SceneSettings.DataSource == SceneDataSource.Device || SceneSettings.DataSource == SceneDataSource.DeviceWithPrefabFallback || SceneSettings.DataSource == SceneDataSource.DeviceWithJsonFallback) &&
                    !Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                {
                    var callbacks = new PermissionCallbacks();
                    callbacks.PermissionDenied += permissionId =>
                    {
                        Debug.LogWarning("User denied permissions to use scene data");
                        // Permissions denied, if data source is using prefab fallback let's load the prefab or Json scene instead
                        if (SceneSettings.DataSource == SceneDataSource.DeviceWithPrefabFallback)
                        {
                            LoadScene(SceneDataSource.Prefab);
                        }
                        else if (SceneSettings.DataSource == SceneDataSource.DeviceWithJsonFallback)
                        {
                            LoadScene(SceneDataSource.Json);
                        }
                    };
                    callbacks.PermissionGranted += permissionId =>
                    {
                        // Permissions are now granted and it is safe to try load the scene now
                        LoadScene(SceneSettings.DataSource);
                    };
                    // Note: If the permission request dialog is already active then this call will silently fail
                    // and we won't receive the callbacks. So as a work-around there is a code in Update() to mitigate
                    // this problem.
                    Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                }
                else
#endif
                {
                    LoadScene(SceneSettings.DataSource);
                }
#pragma warning restore CS4014
            }

            // Activate SceneDebugger when ImmersiveDebugger is enabled.
            if (RuntimeSettings.Instance.ImmersiveDebuggerEnabled && _immersiveSceneDebuggerPrefab != null)
            {
                var sceneDebuggerExist = ImmersiveSceneDebugger.Instance != null;
                if (!sceneDebuggerExist)
                {
                    Instantiate(_immersiveSceneDebuggerPrefab);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                DestroyAnchorStore();
                // Free the shared library when the MRUK instance is destroyed
                MRUKNative.FreeMRUKSharedLibrary();
                Instance = null;
                RoomCreatedEvent.RemoveAllListeners();
                RoomRemovedEvent.RemoveAllListeners();
                RoomUpdatedEvent.RemoveAllListeners();
                SceneLoadedEvent.RemoveAllListeners();
            }
        }

        private void Start()
        {
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.LogPrinter))]
        private static unsafe void OnSharedLibLog(MRUKNativeFuncs.MrukLogLevel logLevel, char* message, uint length)
        {
            try
            {
                LogType type = LogType.Log;
                switch (logLevel)
                {
                    case MRUKNativeFuncs.MrukLogLevel.Debug:
                    case MRUKNativeFuncs.MrukLogLevel.Info:
                        // Unity doesn't have a log level lower than "Log", so use that for both debug and info
                        type = LogType.Log;
                        break;
                    case MRUKNativeFuncs.MrukLogLevel.Warn:
                        type = LogType.Warning;
                        break;
                    case MRUKNativeFuncs.MrukLogLevel.Error:
                        type = LogType.Error;
                        break;
                }

                Debug.LogFormat(type, LogOption.None, null, "MRUK Shared: {0}", Marshal.PtrToStringUTF8((IntPtr)message, (int)length));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.TrackingSpacePoseGetter))]
        private static Pose GetTrackingSpacePose()
        {
            var trackingSpace = GetTrackingSpace();
            Pose trackingSpacePose = trackingSpace != null ? new Pose(trackingSpace.position, trackingSpace.rotation) : Pose.identity;
            var openXrPose = FlipZRotateY180(trackingSpacePose);
            return FlipZRotateY180(Pose.identity).GetTransformedBy(openXrPose);
        }

        [MonoPInvokeCallback(typeof(MRUKNativeFuncs.TrackingSpacePoseSetter))]
        private static void SetTrackingSpacePose(Pose openXrPose)
        {
            var openXrPoseRelativeToIdentity = FlipZRotateY180(Pose.identity).GetTransformedBy(openXrPose);
            var trackingSpacePose = FlipZRotateY180(openXrPoseRelativeToIdentity);
            GetTrackingSpace()?.SetPositionAndRotation(trackingSpacePose.position, trackingSpacePose.rotation);
        }

        private static Transform GetTrackingSpace()
        {
            if (Instance != null && Instance._cameraRig != null)
            {
                return Instance._cameraRig.trackingSpace;
            }
            Debug.LogError(nameof(OVRCameraRig) + " is not present, but MRUK requires it. Please add " + nameof(OVRCameraRig) + " to your scene via 'Meta / Tools / Building Blocks / Camera Rig'.");
            return null;
        }

        private void Update()
        {
            if (SceneSettings.LoadSceneOnStartup && !_loadSceneCalled)
            {
#if !UNITY_EDITOR && UNITY_ANDROID
                // This is to cope with the case where the permissions dialog was already opened before we called
                // Permission.RequestUserPermission in Awake() and we don't get the PermissionGranted callback
                if (Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                {
                    // We can't await for the LoadScene result because Awake is not async, silence the warning
#pragma warning disable CS4014
                    LoadScene(SceneSettings.DataSource);
#pragma warning restore CS4014
                }
#endif
            }

            UpdateAnchorStore();

            bool worldLockActive = false;

            if (_cameraRig)
            {
                if (EnableWorldLock)
                {
                    var room = GetCurrentRoom();
                    if (room)
                    {
                        Pose sharedLibOffset = Pose.identity;
                        if (MRUKNativeFuncs.AnchorStoreGetWorldLockOffset(room.Anchor.Uuid, ref sharedLibOffset))
                        {
                            if (_prevTrackingSpacePose is Pose pose && (_cameraRig.trackingSpace.position != pose.position || _cameraRig.trackingSpace.rotation != pose.rotation))
                            {
                                Debug.LogWarning("MRUK EnableWorldLock is enabled and is controlling the tracking space position.\n" +
                                                 $"Tracking position was set to {_cameraRig.trackingSpace.position} and rotation to {_cameraRig.trackingSpace.rotation}, this is being overridden by MRUK.\n" +
                                                 $"Use '{nameof(TrackingSpaceOffset)}' instead to translate or rotate the TrackingSpace.");
                            }

                            sharedLibOffset = FlipZ(sharedLibOffset);
                            Pose deltaPose;
                            if (room.FloorAnchor is not null && room.FloorAnchor.HasValidHandle)
                            {
                                deltaPose = room.FloorAnchor.DeltaPose;
                            }
                            else
                            {
                                deltaPose = room.DeltaPose;
                            }
                            deltaPose = sharedLibOffset.GetTransformedBy(deltaPose);
                            var position = TrackingSpaceOffset.MultiplyPoint3x4(deltaPose.position);
                            var rotation = TrackingSpaceOffset.rotation * deltaPose.rotation;
                            _cameraRig.trackingSpace.SetPositionAndRotation(position, rotation);
                            _prevTrackingSpacePose = new(position, rotation);
                            worldLockActive = true;
                        }
                    }
                }
                else if (_worldLockWasEnabled)
                {
                    // Reset the tracking space when disabling world lock
                    _cameraRig.trackingSpace.localPosition = Vector3.zero;
                    _cameraRig.trackingSpace.localRotation = Quaternion.identity;
                    _prevTrackingSpacePose = null;
                }

                _worldLockWasEnabled = EnableWorldLock;
            }

            _worldLockActive = worldLockActive;

            UpdateTrackables();
        }

        /// <summary>
        ///     Load the scene asynchronously from the specified data source
        /// </summary>
        internal async Task LoadScene(SceneDataSource dataSource)
        {
            _loadSceneCalled = true;
            try
            {
                if (dataSource == SceneDataSource.Device ||
                    dataSource == SceneDataSource.DeviceWithPrefabFallback ||
                    dataSource == SceneDataSource.DeviceWithJsonFallback)
                {
                    await LoadSceneFromDevice();
                }

                if (dataSource == SceneDataSource.Prefab ||
                    (dataSource == SceneDataSource.DeviceWithPrefabFallback && Rooms.Count == 0))
                {
                    if (SceneSettings.RoomPrefabs.Length == 0)
                    {
                        Debug.LogWarning($"Failed to load room from prefab because prefabs list is empty");
                        return;
                    }

                    // Clone the roomPrefab, but essentially replace all its content
                    // if -1 or out of range, use a random one
                    var roomIndex = GetRoomIndex(true);

                    Debug.Log($"Loading prefab room {roomIndex}");

                    var roomPrefab = SceneSettings.RoomPrefabs[roomIndex];
                    await LoadSceneFromPrefab(roomPrefab);
                }

                if (dataSource == SceneDataSource.Json ||
                    (dataSource == SceneDataSource.DeviceWithJsonFallback && Rooms.Count == 0))
                {
                    if (SceneSettings.SceneJsons.Length != 0)
                    {
                        var roomIndex = GetRoomIndex(false);

                        Debug.Log($"Loading SceneJson {roomIndex}");

                        var ta = SceneSettings.SceneJsons[roomIndex];
                        await LoadSceneFromJsonString(ta.text);
                    }
#pragma warning disable CS0612 // Type or member is obsolete
                    else if (SceneSettings.SceneJson != "")
#pragma warning restore CS0612 // Type or member is obsolete
                    {
#pragma warning disable CS0612 // Type or member is obsolete
                        await LoadSceneFromJsonString(SceneSettings.SceneJson);
#pragma warning restore CS0612 // Type or member is obsolete
                    }
                    else
                    {
                        Debug.LogWarning($"The list of SceneJsons is empty");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }

        private int GetRoomIndex(bool fromPrefabs = true)
        {
            var idx = SceneSettings.RoomIndex;
            if (idx == -1)
            {
                idx = UnityEngine.Random.Range(0, fromPrefabs ? SceneSettings.RoomPrefabs.Length : SceneSettings.SceneJsons.Length);
            }

            return idx;
        }

        /// <summary>
        ///     Called when the room is destroyed
        /// </summary>
        /// <remarks>
        ///     This is used to keep the list of active rooms up to date.
        ///     So there should never be any null entries in the list.
        /// </remarks>
        /// <param name="room"></param>
        internal void OnRoomDestroyed(MRUKRoom room)
        {
            Rooms.Remove(room);
            if (_cachedCurrentRoom == room)
            {
                _cachedCurrentRoom = null;
            }
        }

        /// <summary>
        ///     Destroys the rooms and all children
        /// </summary>
        public void ClearScene()
        {
            ClearSceneSharedLib();
        }

        /// <summary>
        /// Loads the scene based on scene data previously shared with the user via
        /// <see cref="MRUKRoom.ShareRoomAsync"/> or <see cref="ShareRoomsAsync"/>.
        /// </summary>
        /// <remarks>
        ///
        /// This function should be used in co-located multi-player experiences by "guest"
        /// clients that require scene data previously shared by the "host".
        ///
        /// </remarks>
        /// <param name="roomUuids">An optional collection of room anchor UUIDs for which scene data will be loaded from the given group context.
        /// `roomUuids` can be null in which case all rooms from the group context will be loaded, it is the same as calling the <see cref="LoadSceneFromSharedRooms"/>
        /// overload that doesn't contain the `roomUuids` argument. It is more efficient for the host to filter the set of rooms when sharing instead of filtering
        /// the rooms on the guest when loading so in the majority of cases `roomUuids` should be `null`.
        /// </param>
        /// <param name="groupUuid">UUID of the group from which to load the shared rooms.</param>
        /// <param name="alignmentData">Use this parameter to correctly align local and host coordinates when using co-location.<br/>
        /// - `alignmentRoomUuid`: the UUID of the room used for alignment.<br/>
        /// - `floorWorldPoseOnHost`: world-space pose of the FloorAnchor on the host device.<br/>
        /// Using 'null' will disable the alignment, causing the mismatch between the host and the guest. Do this only if your app has custom coordinate alignment.</param>
        /// <param name="removeMissingRooms">
        ///     When enabled, rooms that are already loaded but are not found in the shared group will be removed.
        ///     This is to support the case where a user deletes a room from their device and the change needs to be reflected in the app.
        /// </param>
        /// <returns>An enum indicating whether loading was successful or not.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="groupUuid"/> equals `Guid.Empty`.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="alignmentData.alignmentRoomUuid"/> equals `Guid.Empty`.</exception>
        public async Task<LoadDeviceResult> LoadSceneFromSharedRooms(IEnumerable<Guid> roomUuids, Guid groupUuid, (Guid alignmentRoomUuid, Pose floorWorldPoseOnHost)? alignmentData, bool removeMissingRooms = true)
        {
            if (groupUuid == Guid.Empty)
            {
                throw new ArgumentException(nameof(groupUuid));
            }

            if (alignmentData?.alignmentRoomUuid == Guid.Empty)
            {
                throw new ArgumentException(nameof(alignmentData.Value.alignmentRoomUuid));
            }

            return await LoadSceneFromDeviceInternal(requestSceneCaptureIfNoDataFound: false, removeMissingRooms, new SharedRoomsData { roomUuids = roomUuids, groupUuid = groupUuid, alignmentData = alignmentData });
        }

        /// <summary>
        /// Loads the scene based on scene data previously shared with the user via
        /// <see cref="MRUKRoom.ShareRoomAsync"/> or <see cref="ShareRoomsAsync"/>.
        /// </summary>
        /// <remarks>
        ///
        /// This function should be used in co-located multi-player experiences by "guest"
        /// clients that require scene data previously shared by the "host".
        ///
        /// </remarks>
        /// <param name="groupUuid">UUID of the group from which to load the shared rooms.</param>
        /// <param name="alignmentData">Use this parameter to correctly align local and host coordinates when using co-location.<br/>
        /// - `alignmentRoomUuid`: the UUID of the room used for alignment.<br/>
        /// - `floorWorldPoseOnHost`: world-space pose of the FloorAnchor on the host device.<br/>
        /// Using 'null' will disable the alignment, causing the mismatch between the host and the guest. Do this only if your app has custom coordinate alignment.</param>
        /// <param name="removeMissingRooms">
        ///     When enabled, rooms that are already loaded but are not found in the shared group will be removed.
        ///     This is to support the case where a user deletes a room from their device and the change needs to be reflected in the app.
        /// </param>
        /// <returns>An enum indicating whether loading was successful or not.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="groupUuid"/> equals `Guid.Empty`.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="alignmentData.alignmentRoomUuid"/> equals `Guid.Empty`.</exception>
        public Task<LoadDeviceResult> LoadSceneFromSharedRooms(Guid groupUuid, (Guid alignmentRoomUuid, Pose floorWorldPoseOnHost)? alignmentData, bool removeMissingRooms = true)
        {
            return LoadSceneFromSharedRooms(null, groupUuid, alignmentData, removeMissingRooms);
        }

        private struct SharedRoomsData
        {
            internal IEnumerable<Guid> roomUuids;
            internal Guid groupUuid;
            internal (Guid alignmentRoomUuid, Pose floorWorldPoseOnHost)? alignmentData;
        }

        /// <summary>
        /// Shares multiple MRUK rooms with a group. Note that there is a performance overhead in sharing more rooms than necessary.
        /// Consider sharing only the current room using <see cref="GetCurrentRoom"/> and <see cref="MRUKRoom.ShareRoomAsync"/>.
        /// </summary>
        /// <param name="rooms">A collection of rooms to be shared.</param>
        /// <param name="groupUuid">UUID of the group to which the room should be shared.</param>
        /// <returns>A task that tracks the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rooms"/> is `null`.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="groupUuid"/> equals `Guid.Empty`.</exception>
        public async OVRTask<OVRResult<OVRAnchor.ShareResult>> ShareRoomsAsync(IEnumerable<MRUKRoom> rooms,
            Guid groupUuid)
        {

            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            if (groupUuid == Guid.Empty)
            {
                throw new ArgumentException(nameof(groupUuid));
            }

            using (new OVRObjectPool.ListScope<OVRAnchor>(out var roomAnchors))
            {
                var tasks = new List<OVRTask<bool>>();

                foreach (var room in rooms)
                {
                    if (!room.IsLocal)
                    {
                        Debug.LogError($"Sharing JSON or Prefab rooms is not supported. Only rooms loaded from device ({nameof(MRUKRoom)}.{nameof(MRUKRoom.IsLocal)} == true) can be shared.");
                        return OVRResult<OVRAnchor.ShareResult>.FromFailure(OVRAnchor.ShareResult.FailureOperationFailed);
                    }
                    if (room.Anchor.TryGetComponent<OVRSharable>(out var sharable))
                    {
                        tasks.Add(sharable.SetEnabledAsync(true));
                    }
                    roomAnchors.Add(room.Anchor);
                }

                await OVRTask.WhenAll(tasks);
                return await OVRAnchor.ShareAsync(roomAnchors, groupUuid);
            }
        }

        /// <summary>
        ///     Loads the scene from the data stored on the device.
        /// </summary>
        /// <remarks>
        ///     The user must have granted ScenePermissions or this will fail.
        ///
        ///     In order to check if the user has granted permissions, call
        ///     `Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission)`.
        ///
        ///     In order to request permissions from the user, call
        ///     `Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks)`.
        /// </remarks>
        /// <param name="requestSceneCaptureIfNoDataFound">
        ///     If true and no rooms are found when loading from device,
        ///     the request space setup flow will be started.
        /// </param>
        /// <param name="removeMissingRooms">
        ///     When enabled, rooms that are already loaded but are not found in newSceneData will be removed.
        ///     This is to support the case where a user deletes a room from their device and the change needs to be reflected in the app.
        /// </param>
        /// <returns>An enum indicating whether loading was successful or not.</returns>
        public async Task<LoadDeviceResult> LoadSceneFromDevice(bool requestSceneCaptureIfNoDataFound = true, bool removeMissingRooms = true)
            => await LoadSceneFromDeviceInternal(requestSceneCaptureIfNoDataFound, removeMissingRooms);

        private async Task<LoadDeviceResult> LoadSceneFromDeviceInternal(
            bool requestSceneCaptureIfNoDataFound, bool removeMissingRooms
            , SharedRoomsData? sharedRoomsData = null
        )
        {
            var result = await LoadSceneFromDeviceSharedLib(requestSceneCaptureIfNoDataFound, removeMissingRooms, sharedRoomsData);
            OVRTelemetry.Start(TelemetryConstants.MarkerId.LoadSceneFromDevice)
                .AddAnnotation(TelemetryConstants.AnnotationType.NumRooms, Rooms.Count.ToString())
                .SetResult(result == LoadDeviceResult.Success ? OVRPlugin.Qpl.ResultType.Success : OVRPlugin.Qpl.ResultType.Fail)
                .Send();
            return result;
        }

        private void FindAllObjects(GameObject roomPrefab, out List<GameObject> walls, out List<GameObject> volumes, out List<GameObject> planes)
        {
            walls = new List<GameObject>();
            volumes = new List<GameObject>();
            planes = new List<GameObject>();

            FindObjects(MRUKAnchor.SceneLabels.WALL_FACE.ToString(), roomPrefab.transform, ref walls);
            FindObjects(MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE.ToString(), roomPrefab.transform, ref walls);
            FindObjects(MRUKAnchor.SceneLabels.OTHER.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.TABLE.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.COUCH.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.WINDOW_FRAME.ToString(), roomPrefab.transform, ref planes);
            FindObjects(MRUKAnchor.SceneLabels.DOOR_FRAME.ToString(), roomPrefab.transform, ref planes);
            FindObjects(MRUKAnchor.SceneLabels.WALL_ART.ToString(), roomPrefab.transform, ref planes);
            FindObjects(MRUKAnchor.SceneLabels.PLANT.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.SCREEN.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.BED.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.LAMP.ToString(), roomPrefab.transform, ref volumes);
            FindObjects(MRUKAnchor.SceneLabels.STORAGE.ToString(), roomPrefab.transform, ref volumes);
        }

        /// <summary>
        /// Simulates the creation of a scene in the Editor, using transforms and names from our prefab rooms.
        /// </summary>
        /// <param name="scenePrefab">The prefab GameObject representing the scene or a collection of rooms.</param>
        /// <param name="clearSceneFirst">If true, clears the current scene before loading the new one.</param>
        /// <returns>An enum indicating whether loading was successful or not.</returns>
        public async Task<LoadDeviceResult> LoadSceneFromPrefab(GameObject scenePrefab, bool clearSceneFirst = true)
        {
            if (clearSceneFirst)
            {
                ClearScene();
            }

            var result = await LoadSceneFromPrefabSharedLib(scenePrefab);
            OVRTelemetry.Start(TelemetryConstants.MarkerId.LoadSceneFromPrefab)
                .AddAnnotation(TelemetryConstants.AnnotationType.SceneName, scenePrefab.name)
                .AddAnnotation(TelemetryConstants.AnnotationType.NumRooms, Rooms.Count.ToString())
                .SetResult(result == LoadDeviceResult.Success ? OVRPlugin.Qpl.ResultType.Success : OVRPlugin.Qpl.ResultType.Fail)
                .Send();
            return result;
        }

        /// <summary>
        ///     Serializes the scene data into a JSON string. The scene data includes rooms, anchors, and their associated properties.
        ///     The method allows for the specification of the coordinate system (Unity or Unreal) and whether to include the global mesh data.
        /// </summary>
        /// <param name="coordinateSystem">The coordinate system to use for the serialization (Unity or Unreal).</param>
        /// <param name="includeGlobalMesh">A boolean indicating whether to include the global mesh data in the serialization. Default is true.</param>
        /// <param name="rooms">A list of rooms to serialize, if this is null then all rooms will be serialized.</param>
        /// <returns>A JSON string representing the serialized scene data.</returns>
        [Obsolete("Coordinate system is now obsolete, use the overload that doesn't take this parameter")]
        public string SaveSceneToJsonString(SerializationHelpers.CoordinateSystem coordinateSystem = SerializationHelpers.CoordinateSystem.Unity, bool includeGlobalMesh = true,
            List<MRUKRoom> rooms = null)
        {
            return SaveSceneToJsonString(includeGlobalMesh, rooms);
        }

        /// <summary>
        ///     Serializes the scene data into a JSON string. The scene data includes rooms, anchors, and their associated properties.
        ///     The method allows for the specification of the coordinate system (Unity or Unreal) and whether to include the global mesh data.
        /// </summary>
        /// <param name="includeGlobalMesh">A boolean indicating whether to include the global mesh data in the serialization. Default is true.</param>
        /// <param name="rooms">A list of rooms to serialize, if this is null then all rooms will be serialized.</param>
        /// <returns>A JSON string representing the serialized scene data.</returns>
        public string SaveSceneToJsonString(bool includeGlobalMesh = true, List<MRUKRoom> rooms = null)
        {
            return SaveSceneToJsonSharedLib(includeGlobalMesh, rooms);
        }


        /// <summary>
        ///     Loads the scene from a JSON string representing the scene data.
        /// </summary>
        /// <param name="jsonString">The JSON string containing the serialized scene data.</param>
        /// <param name="removeMissingRooms">When enabled, rooms that are already loaded but are not found in JSON the string will be removed.</param>
        /// <returns>An enum indicating whether loading was successful or not.</returns>
        public async Task<LoadDeviceResult> LoadSceneFromJsonString(string jsonString, bool removeMissingRooms = true)
        {
            var result = await LoadSceneFromJsonSharedLib(jsonString, removeMissingRooms);
            OVRTelemetry.Start(TelemetryConstants.MarkerId.LoadSceneFromJson)
                .AddAnnotation(TelemetryConstants.AnnotationType.NumRooms, Rooms.Count.ToString())
                .SetResult(result == LoadDeviceResult.Success ? OVRPlugin.Qpl.ResultType.Success : OVRPlugin.Qpl.ResultType.Fail)
                .Send();
            return result;
        }

        void FindObjects(string objName, Transform rootTransform, ref List<GameObject> objList)
        {
            if (rootTransform.name.Equals(objName))
            {
                objList.Add(rootTransform.gameObject);
            }

            foreach (Transform child in rootTransform)
            {
                FindObjects(objName, child, ref objList);
            }
        }

    }
}

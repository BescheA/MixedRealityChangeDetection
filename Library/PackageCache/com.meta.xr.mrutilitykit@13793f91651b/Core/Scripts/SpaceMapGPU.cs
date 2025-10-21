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
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    /// Manages the creation and updating of a <see cref="SpaceMap"/> using GPU resources. This class is designed to handle
    /// dynamic environments, providing real-time updates to the spatial map.
    /// </summary>
    public class SpaceMapGPU : MonoBehaviour
    {
        /// <summary>
        /// Event triggered when the space map is initially created
        /// </summary>
        [field: SerializeField]
        public UnityEvent SpaceMapCreatedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        /// Event triggered when the space map is created for a specifc room
        /// </summary>
        public UnityEvent<MRUKRoom> SpaceMapRoomCreatedEvent
        {
            get;
            private set;
        } = new();

        /// <summary>
        /// Event triggered when the space map is updated.
        /// </summary>
        [field: SerializeField]
        public UnityEvent SpaceMapUpdatedEvent
        {
            get;
            private set;
        } = new();

        [Tooltip("When the scene data is loaded, this controls what room(s) the spacemap will run on.")]
        [Header("Scene and Room Settings")]
        public MRUK.RoomFilter CreateOnStart = MRUK.RoomFilter.CurrentRoomOnly;

        [Tooltip("If enabled, updates on scene elements such as rooms and anchors will be handled by this class")]
        internal bool TrackUpdates = true;

        [Space]
        [Header("Textures")]
        [SerializeField]
        [Tooltip("Use this dimension for SpaceMap in X and Y")]
        public int TextureDimension = 512;

        [Tooltip("Colorize the SpaceMap with this Gradient")]
        public Gradient MapGradient = new();

        [Space]
        [Header("SpaceMap Settings")]
        [SerializeField]
        private Material gradientMaterial;

        [SerializeField] private ComputeShader CSSpaceMap;

        [Tooltip("Those Labels will be taken into account when running the SpaceMap")]
        [SerializeField]
        private MRUKAnchor.SceneLabels SceneObjectLabels;

        [Tooltip("Set a color for the inside of an Object")]
        [SerializeField]
        private Color InsideObjectColor;

        [Tooltip("Add this to the border of the capture Camera")]
        [SerializeField]
        private float CameraCaptureBorderBuffer = 0.5f;

        [Space]
        [Header("SpaceMap Debug Settings")]
        [SerializeField]
        [Tooltip("This setting affects your performance. If enabled, the TextureMap will be filled with the SpaceMap")]
        private bool CreateOutputTexture;

        [Tooltip("The Spacemap will be rendered into this Texture.")]
        [SerializeField]
        internal Texture2D OutputTexture;

        [Tooltip("Add here a debug plane")]
        [SerializeField]
        private GameObject DebugPlane;

        [SerializeField] private bool ShowDebugPlane;

        private Color _colorFloorWall = Color.red;
        private Color _colorSceneObjects = Color.green;
        private Color _colorVirtualObjects = Color.blue;

        private Material _matFloor;
        private Material _matObjects;

        private bool _isOrthoCameraInitialized = false;

        private Matrix4x4 _orthoCamProjectionMatrix;
        private Matrix4x4 _orthoCamViewMatrix;
        private Matrix4x4 _orthoCamProjectionViewMatrix;
        private Rect _currentRoomBounds;

        private RenderTexture[] _RTextures = new RenderTexture[2];

        private const string OculusUnlitShader = "Oculus/Unlit";

        private Texture2D _gradientTexture;

        private int _csSpaceMapKernel;
        private int _csFillSpaceMapKernel;
        private int _csPrepareSpaceMapKernel;

        private const string SHADER_GLOBAL_SPACEMAPCAMERAMATRIX = "_SpaceMapProjectionViewMatrix";

        private const float CameraDistance = 10f;
        private const float AspectRatio = 1f;
        private const float NearClipPlane = 0.1f;
        private const float FarClipPlane = 100f;

        [SerializeField]
        private RenderTexture RenderTexture;

        private static readonly int
            WidthID = Shader.PropertyToID("Width"),
            HeightID = Shader.PropertyToID("Height"),
            ColorFloorWallID = Shader.PropertyToID("ColorFloorWall"),
            ColorSceneObjectsID = Shader.PropertyToID("ColorSceneObjects"),
            ColorVirtualObjectsID = Shader.PropertyToID("ColorVirtualObjects"),
            StepID = Shader.PropertyToID("Step"),
            SourceID = Shader.PropertyToID("Source"),
            ResultID = Shader.PropertyToID("Result"),
            SpaceMapCameraMatrixID = Shader.PropertyToID(SHADER_GLOBAL_SPACEMAPCAMERAMATRIX);

        private Dictionary<MRUKRoom, RenderTexture> _roomTextures = new Dictionary<MRUKRoom, RenderTexture>();

        /// <summary>
        /// Gets the <see cref="RenderTexture"/> used for the space map for a given room. This property is not available until the space map is created.
        /// </summary>
        /// <param name="room">The <see cref="MRUKRoom"/> for which to get the space map texture.</param>
        /// <returns></returns>
        public RenderTexture GetSpaceMap(MRUKRoom room = null)
        {
            if (room == null)
            {
                //returning the default RenderTexture which can be initialized with AllRooms or CurrentRoom
                return RenderTexture;
            }
            if (!_roomTextures.TryGetValue(room, out var rt))
            {
                //returning specific RenderTexture if it got called for a specific room
                Debug.Log($"Rendertexture for room {room} not found, returning default texture. Call StartSpaceMap(room) to create a texture for a specific room.");
                return RenderTexture;
            }
            return rt;
        }


        /// <summary>
        /// Initiates the space mapping process based on the specified room filter. This method sets up the necessary components
        /// and configurations to generate the space map, including updating textures and setting up the capture camera.
        /// </summary>
        /// <param name="roomFilter">The <see cref="MRUK.RoomFilter"/> that determines which rooms are included in the space map,
        /// influencing how the space map is generated.</param>
        public void StartSpaceMap(MRUK.RoomFilter roomFilter)
        {
            List<MRUKRoom> rooms;
            switch (roomFilter)
            {
                case MRUK.RoomFilter.None:
                    return;
                case MRUK.RoomFilter.CurrentRoomOnly:
                    rooms = new List<MRUKRoom> { MRUK.Instance.GetCurrentRoom() };
                    break;
                case MRUK.RoomFilter.AllRooms:
                    rooms = MRUK.Instance.Rooms;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(roomFilter), roomFilter, null);
            }

            StartSpaceMapInternal(rooms, RenderTexture);
            SpaceMapCreatedEvent.Invoke();
        }

        /// <summary>
        /// Initiates the space mapping process for a specific room.
        /// </summary>
        /// <param name="room">The <see cref="MRUKRoom"/> that should be used to create the space map.</param>
        public void StartSpaceMap(MRUKRoom room)
        {
            RenderTexture rtRoom;
            if (!_roomTextures.TryGetValue(room, out rtRoom))
            {
                rtRoom = CreateNewRenderTexture(RenderTexture.width);
                _roomTextures[room] = rtRoom;
            }
            StartSpaceMapInternal(new List<MRUKRoom> { room }, rtRoom);
            SpaceMapRoomCreatedEvent.Invoke(room);
        }

        /// <summary>
        /// Color clamps to edge color if worldPosition is off-grid.
        /// getBilinear blends the color between pixels.
        /// </summary>
        /// <param name="worldPosition">The world position to sample the color from.</param>
        /// <returns>The color at the specified world position. Returns black if the capture camera is not initialized.</returns>
        public Color GetColorAtPosition(Vector3 worldPosition)
        {
            if (_currentRoomBounds.size.x <= 0)
                return Color.black;

            var normalizedRectValues = Rect.PointToNormalized(_currentRoomBounds, new Vector2(worldPosition.x, worldPosition.z));
            var rawColor = OutputTexture.GetPixelBilinear(normalizedRectValues.x, normalizedRectValues.y);

            var time = 1 - rawColor.r;
            return rawColor.b > 0 ? InsideObjectColor : MapGradient.Evaluate(time is >= 0 and <= 1 ? time : 0);
        }

        #region Monobehaviour calls
        private void Awake()
        {
            //kernels for compute shader
            _csSpaceMapKernel = CSSpaceMap.FindKernel("SpaceMap");
            _csFillSpaceMapKernel = CSSpaceMap.FindKernel("FillSpaceMap");
            _csPrepareSpaceMapKernel = CSSpaceMap.FindKernel("PrepareSpaceMap");

            _matFloor = new Material(Shader.Find(OculusUnlitShader));
            _matObjects = new Material(Shader.Find(OculusUnlitShader));
            _matFloor.color = _colorFloorWall;
            _matObjects.color = _colorSceneObjects;
        }

        private void Start()
        {
            InitUpdateGradientTexture();
            ApplyMaterial();

            OVRTelemetry.Start(TelemetryConstants.MarkerId.LoadSpaceMapGPU).Send();
        }

        private void OnEnable()
        {
            if (MRUK.Instance is not null)
            {
                MRUK.Instance.RegisterSceneLoadedCallback(SceneLoaded);

                if (TrackUpdates)
                {
                    MRUK.Instance.RoomCreatedEvent.AddListener(ReceiveCreatedRoom);
                    MRUK.Instance.RoomRemovedEvent.AddListener(ReceiveRemovedRoom);
                    MRUK.Instance.RoomUpdatedEvent.AddListener(ReceiveUpdatedRoom);
                }
            }
        }

        private void OnDisable()
        {
            if (MRUK.Instance == null)
            {
                return;
            }

            MRUK.Instance.SceneLoadedEvent.RemoveListener(SceneLoaded);

            if (TrackUpdates)
            {
                MRUK.Instance.RoomCreatedEvent.RemoveListener(ReceiveCreatedRoom);
                MRUK.Instance.RoomRemovedEvent.RemoveListener(ReceiveRemovedRoom);
                MRUK.Instance.RoomUpdatedEvent.RemoveListener(ReceiveUpdatedRoom);
            }
        }

        private void Update()
        {
            Shader.SetGlobalMatrix(SpaceMapCameraMatrixID, _orthoCamProjectionViewMatrix);
            if (DebugPlane != null && DebugPlane.activeSelf != ShowDebugPlane)
            {
                DebugPlane.SetActive(ShowDebugPlane);
            }
        }
        #endregion

        private void StartSpaceMapInternal(List<MRUKRoom> rooms, RenderTexture rt)
        {
            InitializeOrthoCameraMatrixParameters(GetBoundingBox(rooms));
            UpdateBuffer(rooms, rt);
        }

        private void SceneLoaded()
        {
            if (CreateOnStart == MRUK.RoomFilter.None)
            {
                return;
            }

            StartSpaceMap(CreateOnStart);
        }

        private bool IsInitialized()
        {
            return _RTextures[0] != null && _isOrthoCameraInitialized;
        }

        private void UpdateBuffer(MRUKRoom room)
        {
            RenderTexture rtRoom;
            if (!_roomTextures.TryGetValue(room, out rtRoom))
            {
                rtRoom = CreateNewRenderTexture(RenderTexture.width);
                _roomTextures[room] = rtRoom;
            }
            UpdateBuffer(new List<MRUKRoom> { room }, rtRoom);
        }

        private void UpdateBuffer(List<MRUKRoom> rooms, RenderTexture rt)
        {
            CommandBuffer commandBuffer = new CommandBuffer()
            {
                name = "SpaceMap"
            };

            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(1, 1, 1, 1));
            RenderTexture.active = null;

            commandBuffer.SetRenderTarget(rt);

            var wh = TextureDimension;

            if (_RTextures[0] == null || _RTextures[0].width != wh || _RTextures[0].height != wh)
            {
                TryReleaseRT(_RTextures[0]);
                TryReleaseRT(_RTextures[1]);
                _RTextures[0] = CreateNewRenderTexture(wh);
                _RTextures[1] = CreateNewRenderTexture(wh);
            }

            commandBuffer.SetViewProjectionMatrices(_orthoCamViewMatrix, _orthoCamProjectionMatrix);

            DrawRoomsIntoCB(commandBuffer, rooms);
            Graphics.ExecuteCommandBuffer(commandBuffer);

            RunSpaceMap(rt);

            if (CreateOutputTexture)
            {
                RenderTexture.active = rt;
                OutputTexture.ReadPixels(new Rect(0, 0, TextureDimension, TextureDimension), 0, 0);
                OutputTexture.Apply();
                RenderTexture.active = null;
            }

            commandBuffer.Clear();
            commandBuffer.Dispose();
        }

        private void DrawRoomsIntoCB(CommandBuffer commandBuffer, List<MRUKRoom> rooms)
        {
            foreach (var room in rooms)
            {
                {
                    var mesh = Utilities.SetupAnchorMeshGeometry(room.FloorAnchor);
                    commandBuffer.DrawMesh(mesh, room.FloorAnchor.transform.localToWorldMatrix, _matFloor);
                }

                foreach (var anchor in room.Anchors)
                {
                    if (anchor.HasAnyLabel(SceneObjectLabels))
                    {
                        var mesh = Utilities.SetupAnchorMeshGeometry(anchor);
                        commandBuffer.DrawMesh(mesh, anchor.transform.localToWorldMatrix, _matObjects);
                    }
                }
            }
        }

        private void RunSpaceMap(RenderTexture rt)
        {
            CSSpaceMap.SetInt(WidthID, (int)TextureDimension);
            CSSpaceMap.SetInt(HeightID, (int)TextureDimension);
            CSSpaceMap.SetVector(ColorFloorWallID, _colorFloorWall);
            CSSpaceMap.SetVector(ColorSceneObjectsID, _colorSceneObjects);
            CSSpaceMap.SetVector(ColorVirtualObjectsID, _colorVirtualObjects);

            var threadGroupsX = Mathf.CeilToInt(TextureDimension / 8.0f);
            var threadGroupsY = Mathf.CeilToInt(TextureDimension / 8.0f);

            CSSpaceMap.SetTexture(_csPrepareSpaceMapKernel, SourceID, rt);
            CSSpaceMap.SetTexture(_csPrepareSpaceMapKernel, ResultID, _RTextures[0]);
            CSSpaceMap.Dispatch(_csPrepareSpaceMapKernel, threadGroupsX, threadGroupsY, 1);

            var stepAmount = (int)Mathf.Log(TextureDimension, 2);

            int sourceIndex = 0, resultIndex = 0;

            for (var i = 0; i < stepAmount; i++)
            {
                var step = (int)Mathf.Pow(2, stepAmount - i - 1);

                sourceIndex = i % 2;
                resultIndex = (i + 1) % 2;

                CSSpaceMap.SetInt(StepID, step);
                CSSpaceMap.SetTexture(_csSpaceMapKernel, SourceID, _RTextures[sourceIndex]);
                CSSpaceMap.SetTexture(_csSpaceMapKernel, ResultID, _RTextures[resultIndex]);
                CSSpaceMap.Dispatch(_csSpaceMapKernel, threadGroupsX, threadGroupsY, 1);
            }

            //swap indexes to get the correct one for source again
            CSSpaceMap.SetTexture(_csFillSpaceMapKernel, SourceID, _RTextures[resultIndex]);
            CSSpaceMap.SetTexture(_csFillSpaceMapKernel, ResultID, _RTextures[sourceIndex]);
            CSSpaceMap.Dispatch(_csFillSpaceMapKernel, threadGroupsX, threadGroupsY, 1);

            Graphics.Blit(_RTextures[sourceIndex], rt);

            gradientMaterial.SetTexture("_MainTex", rt);
            SpaceMapUpdatedEvent.Invoke();
        }

        private void ReceiveUpdatedRoom(MRUKRoom room)
        {
            if (TrackUpdates)
            {
                RegisterAnchorUpdates(room);
                if (IsInitialized())
                {
                    UpdateBuffer(room);
                }
            }
        }

        private void ReceiveCreatedRoom(MRUKRoom room)
        {
            //only create the effect mesh when we track room updates
            if (TrackUpdates &&
                CreateOnStart == MRUK.RoomFilter.AllRooms)
            {
                RegisterAnchorUpdates(room);
                if (IsInitialized())
                {
                    UpdateBuffer(room);
                }
            }
        }

        private void ReceiveRemovedRoom(MRUKRoom room)
        {
            UnregisterAnchorUpdates(room);
            _roomTextures.Remove(room);
        }

        private void UnregisterAnchorUpdates(MRUKRoom room)
        {
            room.AnchorCreatedEvent.RemoveListener(ReceiveAnchorCreatedEvent);
            room.AnchorRemovedEvent.RemoveListener(ReceiveAnchorRemovedCallback);
            room.AnchorUpdatedEvent.RemoveListener(ReceiveAnchorUpdatedCallback);
        }

        private void RegisterAnchorUpdates(MRUKRoom room)
        {
            room.AnchorCreatedEvent.AddListener(ReceiveAnchorCreatedEvent);
            room.AnchorRemovedEvent.AddListener(ReceiveAnchorRemovedCallback);
            room.AnchorUpdatedEvent.AddListener(ReceiveAnchorUpdatedCallback);
        }

        private void ReceiveAnchorUpdatedCallback(MRUKAnchor anchor)
        {
            // only update the anchor when we track updates
            if (!TrackUpdates)
            {
                return;
            }
            if (IsInitialized())
            {
                UpdateBuffer(anchor.Room);
            }

        }

        private void ReceiveAnchorRemovedCallback(MRUKAnchor anchor)
        {
            // there is no check on ```TrackUpdates``` when removing an anchor.
            if (IsInitialized())
            {
                UpdateBuffer(anchor.Room);
            }
        }

        private void ReceiveAnchorCreatedEvent(MRUKAnchor anchor)
        {
            // only create the anchor when we track updates
            if (!TrackUpdates)
            {
                return;
            }
            if (IsInitialized())
            {
                UpdateBuffer(anchor.Room);
            }
        }

        private static RenderTexture CreateNewRenderTexture(int wh)
        {
            var rt = new RenderTexture(wh, wh, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { enableRandomWrite = true };
            rt.Create();
            return rt;
        }

        private static void TryReleaseRT(RenderTexture renderTexture)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        private void ApplyMaterial()
        {
            gradientMaterial.SetTexture("_GradientTex", _gradientTexture);
            gradientMaterial.SetColor("_InsideColor", InsideObjectColor);
            if (DebugPlane != null)
            {
                DebugPlane.GetComponent<Renderer>().material = gradientMaterial;
            }
        }

        private void InitUpdateGradientTexture()
        {
            if (_gradientTexture == null)
            {
                _gradientTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            }

            for (var i = 0; i <= _gradientTexture.width; i++)
            {
                var t = i / (_gradientTexture.width - 1f);
                _gradientTexture.SetPixel(i, 0, MapGradient.Evaluate(t));
            }

            _gradientTexture.Apply();
        }

        private void InitializeOrthoCameraMatrixParameters(Rect roomBounds)
        {
            _currentRoomBounds = roomBounds;
            var orthoCameraSize = Mathf.Max(roomBounds.width, roomBounds.height) / 2;
            _orthoCamProjectionMatrix = CalculateOrthographicProjMatrix(orthoCameraSize, AspectRatio, NearClipPlane, FarClipPlane);
            _orthoCamViewMatrix = CalculateViewMatrix(); // move camera up _cameraDistance units. Have it look "down".
            _orthoCamProjectionViewMatrix = _orthoCamProjectionMatrix * _orthoCamViewMatrix;

            _isOrthoCameraInitialized = true;

            HandleDebugPlane(roomBounds);
        }

        private Matrix4x4 CalculateOrthographicProjMatrix(float size, float aspect, float near, float far)
        {
            float right = size * aspect;
            float left = -right;
            float top = size;
            float bottom = -top;
            Matrix4x4 orthoMatrix = Matrix4x4.Ortho(left, right, bottom, top, near, far);
            return orthoMatrix;
        }

        private Matrix4x4 CalculateViewMatrix()
        {
            Vector3 orthoCamOffset = new Vector3(_currentRoomBounds.center.x, CameraDistance, _currentRoomBounds.center.y);
            var viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(
              orthoCamOffset,
              Quaternion.Euler(90, 0, 0),
              new Vector3(1, 1, -1)
            ));
            return viewMatrix;
        }

        private Rect GetBoundingBox(List<MRUKRoom> rooms)
        {
            Bounds boundingBox = new Bounds();
            foreach (var room in rooms)
            {
                if (boundingBox.extents != Vector3.zero)
                {
                    boundingBox.Encapsulate(room.GetRoomBounds());
                }
                else
                {
                    boundingBox = room.GetRoomBounds();
                }
            }

            boundingBox.Expand(CameraCaptureBorderBuffer);
            return Rect.MinMaxRect(boundingBox.min.x, boundingBox.min.z, boundingBox.max.x, boundingBox.max.z);
        }

        private void HandleDebugPlane(Rect rect)
        {
            if (DebugPlane == null)
            {
                return;
            }

            var sizeX = (rect.size.x) / 10f;
            var sizeZ = (rect.size.y) / 10f;

            var centerX = rect.center.x;
            var centerZ = rect.center.y;

            if (centerX is float.NaN || centerZ is float.NaN || sizeX is float.NegativeInfinity || sizeZ is float.NegativeInfinity)
            {
                return;
            }

            DebugPlane.transform.localScale = new Vector3(sizeX, 1, sizeZ);
            DebugPlane.transform.position = new Vector3(centerX, DebugPlane.transform.position.y, centerZ);
        }
    }
}

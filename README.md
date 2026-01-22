# Mixed Reality Change Detection (MixedRealityCD)

A Unity-based Mixed Reality application for recording VR sessions, capturing spatial data, and visualizing 3D reconstructions to detect and analyze environmental changes between different scanning sessions.

## Overview

This application enables users to capture and compare physical environments over time using VR headsets (Meta Quest/Oculus). It records RGB images, depth data, and camera poses during VR sessions, then allows users to load and interact with 3D reconstructions generated from those recordings to identify changes in the environment.

### Key Capabilities

- **Session Recording**: Capture RGB images, depth maps, and 6DoF pose data in real-time during VR sessions
- **3D Map Visualization**: Load and display 3D reconstructed meshes from recorded sessions
- **Interactive Comparison**: Overlay, scale, and rotate multiple scans to identify environmental changes
- **Temporal Visualization**: Seamlessly transition between different scan timestamps using a slider interface
- **On-Device Storage**: All recordings saved directly to the VR device for portability

## Application Workflow

### 1. Recording Phase (Data Capture)

The application captures spatial data during VR sessions:

```
VR Session → RGB Frames + Depth Maps + Camera Poses → Local Storage
```

**How it works:**

- The `SessionRecorder` component subscribes to the Meta camera feed via `MetaCameraFeed`
- For each frame, it captures:
  - **RGB Image**: RGBA color data from the camera (saved as PNG)
  - **Depth Map**: Float32 depth values from `MetaDepthProvider` (saved as EXR)
  - **Camera Pose**: 6DoF position, rotation, and intrinsics (saved as JSON)
- All data is timestamped and indexed sequentially
- Files are written asynchronously to prevent frame drops

**Key Components:**

- **`SessionRecorder.cs`**: Main recording orchestrator
  - Manages recording lifecycle (start/stop)
  - Handles frame capture and encoding
  - Writes data to device storage in background thread
  
- **`MetaCameraFeed.cs`**: Interfaces with Meta's camera API
  - Provides access to RGB camera frames
  - Delivers frames with metadata (resolution, timestamp, intrinsics)
  
- **`MetaDepthProvider.cs`**: Accesses depth sensor data
  - Retrieves depth maps from Meta's depth API
  - Provides depth calibration and conversion parameters

**Storage Structure:**
```
/sdcard/Android/data/[PackageName]/files/
  └── session_YYYYMMDD_HHMMSS/
      ├── 000000_color.png
      ├── 000000_pose.json
      ├── 000000_depth.exr
      ├── 000001_color.png
      ├── 000001_pose.json
      ├── 000001_depth.exr
      └── ...
```

### 2. Post-Processing Phase (External)

After recording, data is processed externally to generate 3D reconstructions:

```
Recorded Data → External Processing Pipeline → 3D Meshes + Transform Data
```

This step happens outside the Unity application using tools like:
- Structure-from-Motion (SfM) algorithms
- Multi-View Stereo (MVS) reconstruction
- SLAM-based mapping
- Neural reconstruction methods

**Expected Outputs:**

1. **3D Mesh Files**: Room reconstructions as prefabs/FBX files
2. **RoomTable Asset**: ScriptableObject mapping scan references to mesh prefabs
3. **Transform JSON**: Relative transformations between scans (optional)

### 3. Visualization Phase (Interactive Comparison)

The application loads reconstructed maps and provides interactive tools to analyze changes:

```
3D Meshes + Metadata → Unity Scene → Interactive Visualization
```

**How it works:**

- **Map Loading (`loadMap.cs`)**:
  - Reads `RoomTable` ScriptableObject from Resources folder
  - Loads mesh prefabs for reference scan and all rescans
  - Instantiates all meshes in a parent container with global transform

- **UI Selection (`SelectMapButton.cs`, `LoadMapButton.cs`)**:
  - User selects which scene/map to load via toggle UI
  - Each toggle is linked to a specific scene hash identifier
  - Selected map is loaded and displayed in the viewer

- **Interactive Viewer (`MapViewerController.cs`)**:
  - **Temporal Slider**: Transition between reference and rescan meshes
  - **Rotation Controls**: Rotate map around its geometric center
  - **Scale Controls**: Zoom in/out with clamped limits
  - **Mesh Fading**: Smoothly crossfade between consecutive scans

**Transform Pipeline:**

All scan meshes are positioned in a common coordinate system:

1. **Global Container Transform**:
   - Position: `(-0.2, 1.5, 0.5)` in world space (user's front)
   - Rotation: `(-90°, 0°, 0°)` to align floor plane
   - Scale: `0.1` (10% of original scale for tabletop viewing)

2. **Individual Scan Transforms**:
   - Each scan is instantiated at local position `(0, 0, 0)` within the container
   - All scans share the same coordinate frame

## Code Architecture

### Core Components

#### Recording System

**`SessionRecorder.cs`** - Main recording controller
- Manages recording state machine
- Coordinates frame capture and encoding
- Implements async file writing pipeline
- Key methods:
  - `StartRecording()`: Initializes recording session
  - `StopRecording()`: Finalizes and closes recording
  - `OnCameraFrameUpdate()`: Handles incoming frames
  - `WriterLoop()`: Background thread for file I/O

**`MetaCameraFeed.cs`** - Camera interface
- Wraps Meta Quest camera API
- Delivers RGB frames with calibration data
- Handles camera lifecycle and permissions

**`MetaDepthProvider.cs`** - Depth sensor interface
- Interfaces with Meta's depth estimation
- Provides depth maps and calibration
- Handles depth data format conversions

#### Visualization System

**`loadMap.cs`** - Map loading and scene management
- Loads RoomTable assets from Resources
- Instantiates reference and rescan meshes
- Manages scene lifecycle (load/unload/reset)
- Key methods:
  - `LoadSelectedMap(string mapReference)`: Loads specific map by hash ID
  - `processFlatMap()`: Loads all scans without transformations
  - `ResetScene()`: Cleans up loaded map objects
  - `ResizeParentCollider()`: Adjusts container bounds to fit meshes

**`MapViewerController.cs`** - Interactive visualization
- Implements rotation and scaling controls
- Manages temporal slider for scan transitions
- Handles mesh visibility and crossfading
- Key methods:
  - `Initialize(GameObject container)`: Sets up viewer with map container
  - `OnSliderChange()`: Updates visible meshes based on slider
  - `UpdateMeshFading(float)`: Crossfades between scan timestamps
  - `StartRotateUp/Down/Left/Right()`: Rotation controls
  - `StartScaleUp/Down()`: Scaling controls

**`RoomTable.cs`** - ScriptableObject data container
- Stores references to mesh prefabs
- Maps scene hash IDs to Room objects
- Provides lookup methods for runtime access

#### UI System

**`SelectMapButton.cs`** - Map selection handler
- Detects active toggle in map selection UI
- Triggers map loading for selected scene
- Switches UI from selection to viewer mode

**`LoadMapButton.cs`** - Individual map toggle
- Represents a single map option in the UI
- Stores scene hash reference
- Provides metadata to selection system

**`BackToMainMenuButton.cs`** - Scene cleanup
- Destroys loaded map container
- Resets UI toggles
- Returns to map selection screen

**`ScanRoomButton.cs`** - Room scanning trigger
- Initiates Meta Quest's room setup/capture system
- Integration point for future SLAM-based capture

### Data Structures

**`Room`** - Mesh prefab container
```csharp
public class Room
{
    public string reference;    // Scene/scan hash ID
    public GameObject roomMesh; // Prefab reference
}
```

## Setup and Configuration

### Project Structure

```
Assets/
├── Code/
│   ├── loadMap.cs                    # Main map loader
│   ├── MapViewerController.cs        # Interactive viewer
│   ├── SessionRecorder.cs            # Recording system
│   ├── MetaCameraFeed.cs            # Camera interface
│   ├── MetaDepthProvider.cs         # Depth interface
│   ├── ScriptableObjects/
│   │   └── RoomTable.cs             # Map data container
│   ├── Data/
│   │   └── Room.cs                  # Room data structure
│   └── UI/
│       ├── SelectMapButton.cs       # Map selection
│       ├── LoadMapButton.cs         # Map toggle
│       ├── BackToMainMenuButton.cs  # Navigation
│       └── Button.cs                # UI interface
├── Resources/
│   └── ScriptableObjects/
│       └── [SceneHash]/
│           └── RoomTable.asset      # Scene-specific room data
└── Scenes/
    └── MainScene.unity
```

### Required Assets

1. **RoomTable Setup**:
   - Create RoomTable asset: `Create > ScriptableObjects > RoomTable`
   - Place in: `Resources/ScriptableObjects/[SceneHash]/RoomTable.asset`
   - Add Room entries with references and mesh prefabs

2. **Mesh Prefabs**:
   - Import reconstructed meshes as FBX/OBJ
   - Create prefabs with MeshFilter and MeshRenderer
   - Assign to RoomTable entries

**In Unity Inspector:**

1. **loadMap Component**:
   - `SceneHash`: Default scene identifier
   - `globalRotation`: Container rotation (-90, 0, 0)
   - `spawnPosition`: Container spawn point (-0.2, 1.5, 0.5)
   - `spawnScale`: Display scale (0.1)
   - `mapViewerController`: Reference to viewer component

2. **MapViewerController Component**:
   - `rotationSpeed`: Degrees per second (45)
   - `minScale` / `maxScale`: Scale limits (0.05 - 0.2)
   - `scaleSpeed`: Scale rate (0.1)
   - `timestampSlider`: UI slider reference

3. **SessionRecorder Component**:
   - `infoText`: UI text for recording status
   - `frameQueueCapacity`: Max frames in queue (60)

## Usage Guide

### Recording a Session

1. Start the application on Meta Quest
2. Press the "Record Session" button (if implemented in UI)
3. Move around the environment naturally
4. Press "Stop Recording" when finished
5. Recordings are saved to device storage

### Visualizing Maps

1. Launch the application
2. On the main menu, select a map from the toggle group
3. Press "Select" to load the map
4. Use the interactive controls:
   - **Temporal Slider**: Drag to transition between scans
   - **Rotate Buttons**: Rotate the map in 3D space
   - **Scale Buttons**: Zoom in/out
5. Press "Back to Menu" to return to map selection

### Comparing Scans

1. Load a map with multiple rescans
2. Set slider to 0.0 to view the reference scan
3. Drag slider right to fade in subsequent rescans
4. Rotate and scale to examine specific areas
5. Observe differences in geometry and positioning

## Technical Details

### Transform System

All transformations use Unity's left-handed coordinate system with Y-up. The application uses a simplified transform system where all scans are instantiated at the same local position within the container:

```csharp
// All scans instantiated at local zero
go.transform.localPosition = Vector3.zero;
go.transform.localRotation = Quaternion.identity;
```

### Mesh Visibility System

The temporal slider uses a threshold-based switching system instead of transparency to avoid Z-fighting:

- Slider range: 0.0 to 1.0
- Each mesh occupies an equal segment: `1.0 / (numMeshes)`
- Within each segment, meshes switch at 50% threshold
- Only one mesh is visible at any time (hard switching)
- Reference mesh visible at slider = 0.0

### Recording Pipeline

Frame capture uses a multi-stage asynchronous pipeline:

1. **Capture Stage** (Main Thread):
   - Receives camera frame from Meta API
   - Copies RGB data to avoid synchronization issues
   - Requests async GPU readback for depth data

2. **Encoding Stage** (Main Thread - Update):
   - Encodes RGB to PNG format
   - Encodes depth to EXR format
   - Limited to 4 frames per update to prevent hitches

3. **Writing Stage** (Background Task):
   - Async file I/O for PNG and EXR
   - JSON serialization for pose data
   - Handles cancellation and cleanup

### Platform-Specific Code

Android builds handle RoomTable loading with async coroutines:

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
    // Use coroutine for RoomTable access
    StartCoroutine(LoadMapDataFromStreamingAssets(...));
#else
    // Direct processing for Editor/PC
    processFlatMap();
#endif
```

## Dependencies

- **Unity 2021.3+** (or compatible LTS version)
- **Meta Quest Integration SDK**: Camera and depth APIs
- **XR Interaction Toolkit**: VR input and interaction
- **Universal Render Pipeline (URP)**: Rendering pipeline

## Future Enhancements

Potential extensions mentioned in code comments:

- **Real-time SLAM**: Integration with `ScanRoomButton` for live reconstruction
- **Change Analytics**: Quantitative change detection metrics
- **Multi-user Collaboration**: Shared viewing of reconstructions
- **Cloud Storage**: Upload/download session data

## Contributors

Sotirios Karapiperis, 
Besche Awdir,
Konstantinos Chasiotis,
Tingting Xu

## Supervisor

Anusha Krishnan

---

**Note**: This application is designed for research and development purposes in the field of mixed reality spatial understanding and change detection. For production use, ensure proper testing and validation of the reconstruction pipeline.

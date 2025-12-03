# Data Structure Guide - Change Detection System

## Overview
This project uses multiple JSON and CSV files to visualize 3D scene changes between reference and rescan data. Understanding the data flow is crucial for working with object positions, rotations, and labels.

---

## File Structure & Locations

### 1. **MapData JSON** - `<HashID>.json`
**Location**: `Assets/StreamingAssets/<HashID>.json`

**Purpose**: Contains the main transformation data, scan references, and change detection information.

**Key Fields**:
```json
{
  "reference": "0cac7578-8d6f-2d13-8c2d-bfa7a04f8af3",
  "scans": [
    {
      "reference": "f62fd5f8-9a3f-2f44-8b1e-1289a3a61e26",
      "transform": [/* 16-element 4x4 matrix */],
      "removed": [10, 15, 23],
      "rigid": [
        {
          "instance_reference": 32,
          "instance_rescan": 32,
          "symmetry": 0,
          "transform": [/* 16-element 4x4 matrix */]
        }
      ],
      "nonrigid": [5, 8]
    }
  ],
  "ambiguity": [[/* transformation groups */]]
}
```

**Critical Data**:
- **`reference`**: HashID of the reference (baseline) scan
- **`scans[].reference`**: HashID of each rescan
- **`scans[].transform`**: 4x4 transformation matrix (row-major) to position the entire rescan mesh relative to reference
- **`scans[].removed`**: Array of object IDs that were removed between reference and rescan
- **`scans[].rigid`**: Array of objects with their individual transformation matrices
  - `instance_reference`: Object ID in reference scan
  - `instance_rescan`: Object ID in rescan
  - `transform`: 16-element array representing 4x4 matrix for THIS specific object
- **`ambiguity`**: Groups of transformations for objects with multiple possible matches

---

### 2. **Semantic Segmentation JSON** - `semseg_<HashID>.json`
**Location**: `Assets/StreamingAssets/semseg_<HashID>.json`

**Purpose**: Contains 3D bounding box geometry and labels for each object in a specific scan.

**Key Fields**:
```json
{
  "segGroups": [
    {
      "objectId": 10,
      "id": 10,
      "label": "chair",
      "obb": {
        "centroid": [1.263, -0.379, -1.221],
        "axesLengths": [1.073, 0.765, 1.914],
        "normalizedAxes": [/* 9 elements: 3x3 rotation matrix */]
      },
      "aabb": {
        "min": [x, y, z],
        "max": [x, y, z]
      }
    }
  ]
}
```

**Critical Data**:
- **`objectId`**: Unique identifier matching IDs in MapData
- **`label`**: Semantic label (e.g., "chair", "table", "desk")
- **`obb.centroid`**: **Position** of object center in **local reference scan space**
- **`obb.axesLengths`**: **Size/Scale** (width, height, depth) of oriented bounding box
- **`obb.normalizedAxes`**: **Rotation** as 3x3 matrix (9 floats in row-major order)
- **`aabb`**: Axis-aligned bounding box (fallback if OBB unavailable)

---

### 3. **Ground Truth Labels CSV** - `groundtruth_labels.csv`
**Location**: `Assets/StreamingAssets/groundtruth_labels.csv`

**Purpose**: Shared label database across all scans with additional metadata.

**Format**:
```csv
InstanceID,ClassID,Name,...
10,5,chair,...
15,12,table,...
```

**Critical Data**:
- **`InstanceID`**: Object ID (matches `objectId` in semseg)
- **`ClassID`**: Category classification number
- **`Name`**: Human-readable label (column index 8)

---

## How Position & Rotation Are Derived

### **Reference Scan Objects** (Baseline/Original Position)
**Source**: `semseg_<reference_HashID>.json`

1. **Position**: Directly from `obb.centroid`
   - Unity conversion: `Vector3(-centroid[0], centroid[1], centroid[2])`
   - X-axis is negated for coordinate system alignment

2. **Rotation**: Calculated from `obb.normalizedAxes`
   ```csharp
   // Build 3x3 rotation matrix from 9 floats
   Matrix4x4 rotationMatrix = new Matrix4x4();
   rotationMatrix.m00 = axes[0]; rotationMatrix.m01 = axes[1]; rotationMatrix.m02 = axes[2];
   rotationMatrix.m10 = axes[3]; rotationMatrix.m11 = axes[4]; rotationMatrix.m12 = axes[5];
   rotationMatrix.m20 = axes[6]; rotationMatrix.m21 = axes[7]; rotationMatrix.m22 = axes[8];
   
   // Transpose (row-major to column-major)
   rotationMatrix = rotationMatrix.transpose;
   
   // Convert to Quaternion
   Quaternion rotation = rotationMatrix.rotation;
   
   // Apply axis inversions
   eulerAngles.z = -eulerAngles.z;
   eulerAngles.x = -eulerAngles.x;
   ```

3. **Scale**: Directly from `obb.axesLengths`
   - `Vector3(axesLengths[0], axesLengths[1], axesLengths[2])`

---

### **Rescan Objects** (Transformed Position)
**Source**: `<HashID>.json` → `scans[].rigid[].transform`

Objects that moved between scans have their new position calculated:

1. **Get Reference Transform** (from semseg as above)
2. **Get Rigid Transform Matrix** (from MapData):
   ```csharp
   // rigid.transform is 16-element array (4x4 matrix, row-major)
   Matrix4x4 rigidMatrix = GetMatrixFromFloatArray(rigid.transform);
   ```

3. **Apply Transformation**:
   ```csharp
   Vector3 rescanPosition = rigidMatrix.MultiplyPoint3x4(referencePosition);
   Quaternion rescanRotation = rigidMatrix.rotation * referenceRotation;
   ```

This combines the object's local transformation with the rigid body movement.

---

### **Entire Scan Mesh Positioning**
**Source**: `<HashID>.json` → `scans[].transform`

The complete rescan room mesh is positioned using:
```csharp
Matrix4x4 scanTransform = GetMatrixFromFloatArray(scan.transform);
Vector3 scanPosition = scanTransform.GetPosition();
Quaternion scanRotation = scanTransform.rotation;
```

---

## Data Flow in Code

### Loading Sequence:
1. **`loadMap.LoadSelectedMap(hashID)`**
   - **File**: `Assets/Code/loadMap.cs` (lines ~129-187)
   - Loads `<hashID>.json` → MapData
   - Loads RoomTable from Resources
   - Calls `ProcessMapData()` on successful load

2. **`ChangeDetectionVisualizer.LoadDatabases(hashID)`**
   - **File**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~73-80)
   - Loads `semseg_<hashID>.json` → Object geometry
   - Loads `groundtruth_labels.csv` → Labels
   - Applies manual corrections via `ApplyCorrections()` (lines ~40-61)

3. **`loadMap.ProcessMapData()`**
   - **File**: `Assets/Code/loadMap.cs` (lines ~239-398)
   - Creates ScansContainer with global rotation
   - Instantiates reference room mesh at origin
   - For each rescan:
     - Instantiates rescan mesh with `scan.transform`
     - Calls `VisualizeRemovedObjects()` (red boxes)
     - Calls `VisualizeRigidObjects()` (blue boxes)
   - Calls `AnalyzeAmbiguity()` for mislabel detection

4. **`ChangeDetectionVisualizer.CreateBoundingBox()`**
   - **File**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~540-650)
   - Uses semseg OBB data for initial position/rotation/scale
   - Stores as "reference" transform in `objectTransforms` dictionary
   - Optionally applies manual corrections from `corrections` dictionary (lines ~18-35)

5. **`ChangeDetectionVisualizer.AssignRescanTransforms()`**
   - **File**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~683-710)
   - Iterates through MapData rigid transforms
   - Calculates rescan position: `rigidMatrix * referencePosition`
   - Stores both reference and rescan transforms
   - Calls `RefreshBoundingBoxesTransform()` to apply changes

6. **`ChangeDetectionVisualizer.RefreshBoundingBoxesTransform()`**
   - **File**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~715-740)
   - Toggles between reference and rescan positions based on `showRescanBoundingBoxes`
   - Updates all bounding box transforms in real-time

---

## Key Classes & Data Structures

### C# Classes (`loadMap.cs`):
**Location**: `Assets/Code/loadMap.cs` (lines ~10-48)

```csharp
public class MapData {
    public string reference;
    public List<ScanData> scans;
    public List<List<TransformData>> ambiguity;
}

public class ScanData {
    public string reference;
    public float[] transform;      // 16 elements
    public List<int> removed;
    public List<RigidTransform> rigid;
    public List<int> nonrigid;
}

public class RigidTransform {
    public int instance_reference;
    public int instance_rescan;
    public int symmetry;
    public float[] transform;      // 16 elements
}
```

### C# Classes (`ChangeDetectionVisualizer.cs`):
**Location**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~10-48)

```csharp
public class SegGroupData {
    public int objectId;
    public string label;
    public OBBData obb;
    public AABBData aabb;
}

public class OBBData {
    public float[] centroid;       // 3 elements [x,y,z]
    public float[] axesLengths;    // 3 elements [width,height,depth]
    public float[] normalizedAxes; // 9 elements (3x3 rotation)
}
```

### Manual Corrections System:
**Location**: `Assets/Code/ChangeDetectionVisualizer.cs` (lines ~18-35)

```csharp
private readonly Dictionary<int, ObjectCorrection> corrections = new Dictionary<int, ObjectCorrection>
{
    { 10, new ObjectCorrection {
        labelOverride = "table",
        position = new Vector3(1.263f, -0.379f, -1.221f),
        rotationEuler = new Vector3(272.15686f, 90f, 270f),
        scale = new Vector3(1.073f, 0.765f, 1.914f)
    }}
};
```

---

## Matrix Conversion (Row-Major → Unity Column-Major)

**Location**: `Assets/Code/loadMap.cs` → `GetMatrixFromFloatArray()` (lines ~465-480)

The JSON stores matrices in **row-major** order (standard mathematical notation), but Unity uses **column-major**. We transpose during loading:

```csharp
Matrix4x4 matrix = new Matrix4x4();
// Fill row-major
matrix.m00 = arr[0];  matrix.m01 = arr[1];  ... matrix.m03 = arr[3];
matrix.m10 = arr[4];  matrix.m11 = arr[5];  ... matrix.m13 = arr[7];
matrix.m20 = arr[8];  matrix.m21 = arr[9];  ... matrix.m23 = arr[11];
matrix.m30 = arr[12]; matrix.m31 = arr[13]; ... matrix.m33 = arr[15];

// Transpose to column-major for Unity
return matrix.transpose;
```

---

## Coordinate System Conversions

### Why Negate X-Axis?
```csharp
Vector3 center = new Vector3(
    -segGroup.obb.centroid[0],  // Negated!
    segGroup.obb.centroid[1],
    segGroup.obb.centroid[2]
);
```
The data uses a different coordinate system convention. Negating X aligns it with Unity's left-handed coordinate system.

### Why Invert Rotation Axes?
```csharp
eulerAngles.z = -eulerAngles.z;
eulerAngles.x = -eulerAngles.x;
```
Compensates for coordinate system handedness differences between the data source and Unity.

---

## Inspector Toggles & Features

### `showRescanBoundingBoxes` (ChangeDetectionVisualizer)
- **false**: Shows objects in reference scan position (from semseg OBB)
- **true**: Shows objects in rescan position (OBB transformed by rigid matrix)

### `enableDebugLogs` (loadMap & ChangeDetectionVisualizer)
- Logs all transform matrices, positions, rotations
- Shows rigid transform extraction details
- Displays ambiguity analysis warnings

### `visualizeRemovedObjects` (loadMap)
- **true**: Creates red bounding boxes for removed objects
- Also creates blue boxes for rigid objects when enabled

---

## Common Debugging Steps

1. **Object at wrong position?**
   - Check if `showRescanBoundingBoxes` is set correctly
   - Verify `semseg_<HashID>.json` has correct `obb.centroid`
   - Check if rigid transform exists in MapData

2. **Wrong label?**
   - Check manual corrections dictionary (ID 10 → "table")
   - Verify `groundtruth_labels.csv` and `semseg.label` match

3. **Wrong rotation?**
   - Inspect `obb.normalizedAxes` (should be 9 floats)
   - Check coordinate system inversions (X/Z negation)
   - Verify matrix transpose is applied

4. **Object not visualized?**
   - Ensure objectId exists in both semseg and MapData
   - Check if ID is in `removed` or `rigid` arrays
   - Verify RoomTable contains the scan reference

---

## Example: Tracing Object ID 10

1. **MapData** (`f62fd5f8-9a3f-2f44-8b1e-1289a3a61e26.json`):
   ```json
   "rigid": [{
     "instance_reference": 10,
     "transform": [0.752, -0.654, 0.074, 0, ...]
   }]
   ```

2. **Semseg** (`semseg_0cac7578-8d6f-2d13-8c2d-bfa7a04f8af3.json`):
   ```json
   {
     "objectId": 10,
     "label": "chair",
     "obb": {
       "centroid": [1.263, -0.379, -1.221],
       "axesLengths": [1.073, 0.765, 1.914]
     }
   }
   ```

3. **Manual Correction** (in code):
   ```csharp
   { 10, new ObjectCorrection {
       labelOverride = "table",
       position = new Vector3(1.263f, -0.379f, -1.221f),
       rotationEuler = new Vector3(272.15686f, 90f, 270f),
       scale = new Vector3(1.073f, 0.765f, 1.914f)
   }}
   ```

4. **Result**: ID 10 displays as "table" with corrected transform, toggleable between reference and rescan positions.

---

## Quick Reference

| Data Need | Primary Source | Secondary Source | Code Location |
|-----------|---------------|------------------|---------------|
| Object Position (Reference) | `semseg_<HashID>.json` → `obb.centroid` | - | `CreateBoundingBox()` |
| Object Position (Rescan) | `<HashID>.json` → `rigid.transform` | Applied to reference | `AssignRescanTransforms()` |
| Object Rotation | `semseg_<HashID>.json` → `obb.normalizedAxes` | - | `CreateBoundingBox()` |
| Object Scale | `semseg_<HashID>.json` → `obb.axesLengths` | - | `CreateBoundingBox()` |
| Object Label | `semseg_<HashID>.json` → `label` | `groundtruth_labels.csv` | `LoadDatabases()` |
| Removed Objects | `<HashID>.json` → `scans[].removed` | - | `VisualizeRemovedObjects()` |
| Scan Transform | `<HashID>.json` → `scans[].transform` | - | `ProcessMapData()` |

---

**Note**: All file paths use forward slashes (`/`) and are automatically normalized across platforms. StreamingAssets files are loaded using `UnityWebRequest` on Android builds.

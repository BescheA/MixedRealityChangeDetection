using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// JSON data structures for change detection
/// </summary>
[System.Serializable]
public class TransformData
{
    public int instance_source;
    public int instance_target;
    public float[] transform;
}

[System.Serializable]
public class AmbiguityGroup
{
    public List<TransformData> group;
}

[System.Serializable]
public class RigidTransform
{
    public int instance_reference;
    public int instance_rescan;
    public int symmetry;
    public float[] transform;
}

[System.Serializable]
public class ScanData
{
    public List<int> nonrigid;
    public string reference;
    public List<int> removed;
    public List<RigidTransform> rigid;
    public float[] transform;
}

[System.Serializable]
public class MapData
{
    public List<List<TransformData>> ambiguity;
    public string reference;
    public List<ScanData> scans;
}

public class loadMap : MonoBehaviour
{
    private GameObject roomMesh;
    [Header("Provide Scene Hash for automatic RoomTable loading")]
    public string SceneHash;
    private RoomTable RoomTable;

    [Header("Input Action for Loading Map")]
    public InputActionReference loadMapAction;
    
    [Header("Global Rotation for All Scans")]
    public Vector3 globalRotation = new Vector3(-90, 0, 0);
    
    [Header("Transform Options")]
    public bool useInverseTransform = false;
    public bool invertPositionX = false;
    public bool invertRotationZ = false;
    
    [Header("Change Detection Visualization")]
    public ChangeDetectionVisualizer changeDetectionVisualizer;
    public bool visualizeRemovedObjects = true;
    
    [Header("Debug Options")]
    public bool enableDebugLogs = true;
    
    private GameObject scansContainer;
    
    void Start()
    {
        
    }
    private void OnEnable()
    {
        if (loadMapAction != null)
        {
            loadMapAction.action.performed += OnLoadMapAction;
            loadMapAction.action.Enable();
            if (enableDebugLogs) Debug.Log("LoadMapAction enabled");
        }
    }
    private void OnDisable()
    {
        if (loadMapAction != null)
        {
            loadMapAction.action.performed -= OnLoadMapAction;
            loadMapAction.action.Disable();
            if (enableDebugLogs) Debug.Log("LoadMapAction disabled");
        }
    }

    private void OnLoadMapAction(InputAction.CallbackContext context)
    {
        LoadSelectedMap(SceneHash);
    }

    /// <summary>
    /// Loads a map with change detection data for the specified scene hash
    /// </summary>
    /// <param name="mapReference">Hash ID of the scene to load</param>
    public void LoadSelectedMap(string mapReference) 
    {   
        Debug.Log($"[LoadMap] LoadSelectedMap called with mapReference: '{mapReference}'");
        
        if(string.IsNullOrEmpty(mapReference))
        {
            mapReference = "0cac7578-8d6f-2d13-8c2d-bfa7a04f8af3";
            Debug.Log($"[LoadMap] mapReference was empty, using default: {mapReference}");
        }
        
        // Auto-load RoomTable from Resources folder (works both in Editor and on Device)
        if (RoomTable == null)
        {
            string roomTablePath = $"ScriptableObjects/{mapReference}/RoomTable";
            Debug.Log($"[LoadMap] Attempting to load RoomTable from: Resources/{roomTablePath}");
            RoomTable = Resources.Load<RoomTable>(roomTablePath);
            
            if (RoomTable != null)
            {
                Debug.Log($"[LoadMap] Successfully loaded RoomTable from Resources: {roomTablePath}");
            }
            else
            {
                Debug.LogError($"[LoadMap] RoomTable not found at Resources/{roomTablePath}");
                Debug.LogError($"[LoadMap] Please ensure Assets/Resources/ScriptableObjects/{mapReference}/RoomTable.asset exists");
                
                // Try to list what's actually in Resources
                UnityEngine.Object[] allResources = Resources.LoadAll("ScriptableObjects", typeof(RoomTable));
                Debug.LogError($"[LoadMap] Found {allResources.Length} RoomTable assets in Resources/ScriptableObjects/");
                foreach (var res in allResources)
                {
                    Debug.LogError($"[LoadMap]   - {res.name}");
                }
            }
        }
        else
        {
            Debug.Log($"[LoadMap] RoomTable already assigned, skipping auto-load");
        }
        
        string mapDataPath = Path.Combine(Application.streamingAssetsPath, $"{mapReference}.json");
        if (enableDebugLogs) Debug.Log($"Loading map JSON from: {mapDataPath}");
#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, load RoomTable in coroutine before processing data
        StartCoroutine(LoadMapDataFromStreamingAssets(Path.GetFileName(mapDataPath), mapReference));
        return;
#else
        string fullPath = mapDataPath;
        
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"JSON file not found: {fullPath}");
            Debug.LogError($"Ensure the file exists in Assets/StreamingAssets/{Path.GetFileName(mapDataPath)}");
            return;
        }

        MapData mapData = LoadMapData(fullPath);
        ProcessMapData(mapData, mapReference);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator LoadMapDataFromStreamingAssets(string relativePath, string mapReference)
    {
        // Load RoomTable first if not already loaded
        if (RoomTable == null)
        {
            string roomTablePath = $"ScriptableObjects/{mapReference}/RoomTable";
            Debug.Log($"[LoadMap-Android] Attempting to load RoomTable from: Resources/{roomTablePath}");
            RoomTable = Resources.Load<RoomTable>(roomTablePath);
            
            if (RoomTable != null)
            {
                Debug.Log($"[LoadMap-Android] Successfully loaded RoomTable from Resources: {roomTablePath}");
            }
            else
            {
                Debug.LogError($"[LoadMap-Android] RoomTable not found at Resources/{roomTablePath}");
                Debug.LogError($"[LoadMap-Android] Please ensure Assets/Resources/ScriptableObjects/{mapReference}/RoomTable.asset exists");
                
                // Try to list what's actually in Resources
                UnityEngine.Object[] allResources = Resources.LoadAll("ScriptableObjects", typeof(RoomTable));
                Debug.LogError($"[LoadMap-Android] Found {allResources.Length} RoomTable assets in Resources/ScriptableObjects/");
                foreach (var res in allResources)
                {
                    Debug.LogError($"[LoadMap-Android]   - {res.name}");
                }
            }
        }
        
        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"JSON file not found: {fullPath}");
                Debug.LogError($"Error: {www.error}");
                yield break;
            }
            
            MapData mapData = JsonUtility.FromJson<MapData>(www.downloadHandler.text);
            ProcessMapData(mapData, mapReference);
        }
    }
#endif

    /// <summary>
    /// Processes loaded map data and instantiates room meshes with transformations
    /// </summary>
    /// <param name="mapData">The loaded map data containing scans and transformations</param>
    /// <param name="mapReference">Hash ID of the scene</param>
    private void ProcessMapData(MapData mapData, string mapReference)
    {
        
        if (mapData == null)
        {
            Debug.LogError("MapData could not be loaded!");
            return;
        }
        
        if (changeDetectionVisualizer != null && visualizeRemovedObjects)
        {
            changeDetectionVisualizer.LoadDatabases(mapReference);
        }
        
        scansContainer = new GameObject($"ScansContainer_{mapReference}");
        scansContainer.transform.position = Vector3.zero;
        scansContainer.transform.rotation = Quaternion.Euler(globalRotation);
        
        // Add manipulation script for grabbing, rotating, and scaling
        var manipulationScript = scansContainer.AddComponent<ScansContainerManipulation>();
        manipulationScript.enableDebugLogs = enableDebugLogs;
        
        if (enableDebugLogs) Debug.Log($"Scans container created with global rotation: {globalRotation}");

        Debug.Log($"[ProcessMapData] RoomTable is null: {RoomTable == null}");
        Debug.Log($"[ProcessMapData] mapData.reference: '{mapData.reference}'");
        Debug.Log($"[ProcessMapData] mapReference (parameter): '{mapReference}'");

        if (RoomTable != null && !string.IsNullOrEmpty(mapReference))
        {
            Room room = RoomTable.GetRoomByReference(mapReference);
            if (room != null && room.roomMesh != null)
            {
                roomMesh = room.roomMesh;
                var go = Instantiate(roomMesh, Vector3.zero, Quaternion.identity, scansContainer.transform);
                go.transform.localRotation = Quaternion.Euler(Vector3.zero);
                if (enableDebugLogs) Debug.Log($"Instantiated initial reference: {mapReference} at position (0,0,0), is position: {go.transform.position} | Rotation: {go.transform.rotation}");
            }
            else
            {
                Debug.LogError($"Room or room mesh not found in RoomTable for reference: {mapReference}. Please ensure RoomTable is assigned and contains the reference.");
                return;
            }
        }
        else if (roomMesh != null)
        {
            if (enableDebugLogs) Debug.Log("Instantiating default reference mesh at position (0,0,0)");
            Instantiate(roomMesh, Vector3.zero, Quaternion.identity, scansContainer.transform);
        }
        else
        {
            Debug.LogError("Cannot instantiate reference mesh: RoomTable is null and no default roomMesh assigned. Please assign RoomTable or roomMesh in Inspector.");
            return;
        }

        if (RoomTable != null && mapData.scans != null) 
        {
            Debug.Log($"[ProcessMapData] Processing {mapData.scans.Count} scans");
            foreach (var scan in mapData.scans) 
            {
                Debug.Log($"[ProcessMapData] Processing scan with reference: '{scan.reference}'");
                
                if (RoomTable.GetRoomByReference(scan.reference) == null)
                {
                    Debug.LogWarning($"Scan {scan.reference} not found in RoomTable - skipping");
                    continue;
                }
                
                GameObject scanRoomMesh = RoomTable.GetRoomByReference(scan.reference).roomMesh;
                
                if (scanRoomMesh == null)
                {
                    if (enableDebugLogs) Debug.LogWarning($"No room mesh found for reference: {scan.reference} - skipping");
                    continue;
                }

                if (GameObject.Find(scanRoomMesh.name) != null)
                {
                    if (enableDebugLogs) Debug.Log($"Room Mesh {scanRoomMesh.name} already instantiated - skipping");
                    continue;
                }

                if (scan.transform != null && scan.transform.Length == 16)
                {
                    try
                    {
                        Matrix4x4 transformMatrix = GetMatrixFromFloatArray(scan.transform);
                        
                        if (useInverseTransform)
                        {
                            transformMatrix = transformMatrix.inverse;
                        }
                        
                        Vector3 position = transformMatrix.GetPosition();
                        Quaternion rotation = transformMatrix.rotation;
                        
                        if (invertPositionX)
                        {
                            position.x = -position.x;
                        }
                        
                        Vector3 eulerAngles = rotation.eulerAngles;
                        if (invertRotationZ)
                        {
                            eulerAngles.z = -eulerAngles.z;
                        }
                        rotation = Quaternion.Euler(eulerAngles);
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Rescan {scan.reference}:");
                            Debug.Log($"  UseInverse: {useInverseTransform}, InvertX: {invertPositionX}, InvertRotZ: {invertRotationZ}");
                            Debug.Log($"  Original Transform Matrix (row-major):");
                            Debug.Log($"    [{scan.transform[0]:F4}, {scan.transform[1]:F4}, {scan.transform[2]:F4}, {scan.transform[3]:F4}]");
                            Debug.Log($"    [{scan.transform[4]:F4}, {scan.transform[5]:F4}, {scan.transform[6]:F4}, {scan.transform[7]:F4}]");
                            Debug.Log($"    [{scan.transform[8]:F4}, {scan.transform[9]:F4}, {scan.transform[10]:F4}, {scan.transform[11]:F4}]");
                            Debug.Log($"    [{scan.transform[12]:F4}, {scan.transform[13]:F4}, {scan.transform[14]:F4}, {scan.transform[15]:F4}]");
                            Debug.Log($"  Calculated Position: {position}");
                            Debug.Log($"  Calculated Rotation: {rotation.eulerAngles}");
                        }
                        
                        var go = Instantiate(scanRoomMesh, scansContainer.transform);
                        go.transform.localPosition = position;
                        go.transform.localRotation = rotation;
                        if (enableDebugLogs) Debug.Log($"Instantiated rescan object local position: {go.transform.localPosition} | global position: {go.transform.position}");
                        
                        if (changeDetectionVisualizer != null && visualizeRemovedObjects && scan.removed != null && scan.removed.Count > 0)
                        {
                            if (enableDebugLogs) Debug.Log($"Visualizing {scan.removed.Count} removed objects for scan {scan.reference}");
                            changeDetectionVisualizer.VisualizeRemovedObjects(scan.removed, scansContainer.transform, scan.reference);
                        }
                    }
                    catch (Exception e)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"Error processing scan {scan.reference}: {e.Message}");
                    }
                }
                else
                {
                    if (enableDebugLogs) Debug.LogWarning($"No global transformation found for scan: {scan.reference} - skipping");
                }
            }
        }
    }

    /// <summary>
    /// Loads map data from a JSON file
    /// </summary>
    /// <param name="jsonPath">Absolute path to the JSON file</param>
    /// <returns>Parsed MapData or null if loading failed</returns>
    private MapData LoadMapData(string jsonPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            MapData data = JsonUtility.FromJson<MapData>(jsonContent);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading JSON file: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts a float array (16 elements, row-major) to Unity Matrix4x4 (column-major)
    /// </summary>
    /// <param name="matrixArray">16-element float array in row-major format</param>
    /// <returns>Transposed Matrix4x4 for Unity</returns>
    private Matrix4x4 GetMatrixFromFloatArray(float[] matrixArray)
    {
        if (matrixArray == null || matrixArray.Length != 16)
        {
            Debug.LogError("Transform array must contain 16 elements!");
            return Matrix4x4.identity;
        }

        Matrix4x4 matrix = new Matrix4x4();
        
        matrix.m00 = matrixArray[0];  matrix.m01 = matrixArray[1];  matrix.m02 = matrixArray[2];  matrix.m03 = matrixArray[3];
        matrix.m10 = matrixArray[4];  matrix.m11 = matrixArray[5];  matrix.m12 = matrixArray[6];  matrix.m13 = matrixArray[7];
        matrix.m20 = matrixArray[8];  matrix.m21 = matrixArray[9];  matrix.m22 = matrixArray[10]; matrix.m23 = matrixArray[11];
        matrix.m30 = matrixArray[12]; matrix.m31 = matrixArray[13]; matrix.m32 = matrixArray[14]; matrix.m33 = matrixArray[15];
        
        return matrix.transpose;
    }

    /// <summary>
    /// Finds a specific transformation based on instance IDs
    /// </summary>
    /// <param name="mapData">The map data to search</param>
    /// <param name="sourceInstance">Source instance ID</param>
    /// <param name="targetInstance">Target instance ID</param>
    /// <returns>Transformation matrix or identity if not found</returns>
    public Matrix4x4 GetTransformForInstances(MapData mapData, int sourceInstance, int targetInstance)
    {
        // Suche in rigid transforms
        foreach (var scan in mapData.scans)
        {
            foreach (var rigid in scan.rigid)
            {
                if (rigid.instance_reference == sourceInstance && rigid.instance_rescan == targetInstance)
                {
                    return GetMatrixFromFloatArray(rigid.transform);
                }
            }
        }

        // Suche in ambiguity groups
        foreach (var group in mapData.ambiguity)
        {
            foreach (var transform in group)
            {
                if (transform.instance_source == sourceInstance && transform.instance_target == targetInstance)
                {
                    return GetMatrixFromFloatArray(transform.transform);
                }
            }
        }

        Debug.LogWarning($"No transformation found for Source: {sourceInstance}, Target: {targetInstance}");
        return Matrix4x4.identity;
    }
}
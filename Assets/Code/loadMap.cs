using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;


/**
        Position of Map: -0.2, 1.5, 0.5
        Rotation of Map: -90, 0, 0
        Scale of Map: 0.1, 0.1, 0.1
**/
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
    public Dictionary<string, bool> Scenes;
    private RoomTable RoomTable;

    [Header("Input Action for Loading Map")]
    public InputActionReference loadMapAction;
    
    [Header("Input Actions for Container Manipulation")]
    public InputActionReference rightThumbstickAction;
    public InputActionReference leftThumbstickAction;
    
    [Header("Global Rotation for All Scans")]
    public Vector3 globalRotation = new Vector3(-90, 0, 0);
    
    [Header("Spawn Transform Options")]
    public Vector3 spawnPosition = new Vector3(-0.2f, 1.5f, 0.5f);
    public float spawnScale = 0.1f;
    
    [Header("Transform Options")]
    public bool useInverseTransform = false;
    public bool invertPositionX = false;
    public bool invertRotationZ = false;
    
    [Header("Map Viewer Controller")]
    public MapViewerController mapViewerController;
    
    [Header("Change Detection Visualization")]
    public ChangeDetectionVisualizer changeDetectionVisualizer;
    public bool visualizeRemovedObjects = true;
    
    [Header("Debug Options")]
    public bool enableDebugLogs = true;
    
    private GameObject scansContainer;
    public List<GameObject> rescanObjects;
    public GameObject referenceObject;
    void Start()
    {
        Scenes = new Dictionary<string, bool>()
        {
            {"0cac7578-8d6f-2d13-8c2d-bfa7a04f8af3", false},
            {"f62fd5f8-9a3f-2f44-8b1e-1289a3a61e26", true}
        };
        foreach (var scene in Scenes)
        {
            if (scene.Value)
            {
                SceneHash = scene.Key;
                if (enableDebugLogs) Debug.Log($"[LoadMap] Selected SceneHash: {SceneHash}");
                break;
            }
        }
    }
    private void OnEnable()
    {
        /*if (loadMapAction != null)
        {
            loadMapAction.action.performed += OnLoadMapAction;
            loadMapAction.action.Enable();
            if (enableDebugLogs) Debug.Log("LoadMapAction enabled");
        }*/
    }
    private void OnDisable()
    {
        /*if (loadMapAction != null)
        {
            loadMapAction.action.performed -= OnLoadMapAction;
            loadMapAction.action.Disable();
            if (enableDebugLogs) Debug.Log("LoadMapAction disabled");
        }*/
    }

    private void OnLoadMapAction(InputAction.CallbackContext context)
    {
        LoadSelectedMap(SceneHash);
    }
    /// <summary>
    /// Setzt die Szene komplett zurück: entfernt ScansContainer, BoundingBoxes, Rescan-Objekte und das ReferenceObject.
    /// </summary>
    public void ResetScene()
    {
        if (scansContainer != null)
        {
            Destroy(scansContainer);
            scansContainer = null;
            if (enableDebugLogs) Debug.Log("[ResetScene] ScansContainer entfernt.");
        }
        if (changeDetectionVisualizer != null)
        {
            changeDetectionVisualizer.ClearBoundingBoxes();
            if (enableDebugLogs) Debug.Log("[ResetScene] BoundingBoxes entfernt.");
        }
        if (rescanObjects != null && rescanObjects.Count > 0)
        {
            foreach (var obj in rescanObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            rescanObjects.Clear();
            if (enableDebugLogs) Debug.Log("[ResetScene] rescanObjects entfernt und Liste geleert.");
        }
        if (referenceObject != null)
        {
            Destroy(referenceObject);
            referenceObject = null;
            if (enableDebugLogs) Debug.Log("[ResetScene] referenceObject entfernt.");
        }
    }
    /// <summary>
    /// Loads a map with change detection data for the specified scene hash
    /// </summary>
    /// <param name="mapReference">Hash ID of the scene to load</param>
    public void LoadSelectedMap(string mapReference) 
    {   
        Debug.Log($"[LoadMap] LoadSelectedMap called with mapReference: '{mapReference}'");

        // --- PATCH: Remove old containers before loading new map ---
        if (scansContainer != null)
        {
            Destroy(scansContainer);
            scansContainer = null;
            if (enableDebugLogs) Debug.Log("[LoadMap] Destroyed old ScansContainer before loading new map");
        }
        if (changeDetectionVisualizer != null)
        {
            changeDetectionVisualizer.ClearBoundingBoxes();
            if (enableDebugLogs) Debug.Log("[LoadMap] Cleared old bounding boxes before loading new map");
        }

        
        if(string.IsNullOrEmpty(mapReference))
        {
            mapReference = "0cac7578-8d6f-2d13-8c2d-bfa7a04f8af3";
            Debug.Log($"[LoadMap] mapReference was empty, using default: {mapReference}");
        }
        
        // Auto-load RoomTable from Resources folder (works both in Editor and on Device)
        // Always reload to ensure correct RoomTable for the current mapReference
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
        
        string mapDataPath = Path.Combine(Application.streamingAssetsPath, $"{mapReference}.json");
        if (enableDebugLogs) Debug.Log($"Loading map JSON from: {mapDataPath}");
#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, load RoomTable in coroutine before processing data
        StartCoroutine(LoadMapDataFromStreamingAssets(Path.GetFileName(mapDataPath), mapReference));
        return;
#else
        if(mapReference.Equals("flat")) {
            processFlatMap();
        } else
        {        
            string fullPath = mapDataPath;
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"JSON file not found: {fullPath}");
                Debug.LogError($"Ensure the file exists in Assets/StreamingAssets/{Path.GetFileName(mapDataPath)}");
                return;
            }
            MapData mapData = LoadMapData(fullPath);
            ProcessMapData(mapData, mapReference);
        }
#endif
    }

    private void processFlatMap()
    {
        if (scansContainer != null)
        {
            Destroy(scansContainer);
            if (enableDebugLogs) Debug.Log("[ProcessMapData] Destroyed old ScansContainer");
        }
        
        scansContainer = new GameObject($"ScansContainer_flat");
        scansContainer.transform.position = spawnPosition;
        scansContainer.transform.rotation = Quaternion.Euler(globalRotation);
        scansContainer.transform.localScale = Vector3.one * spawnScale;


        if (RoomTable != null)
        {
            Room room = RoomTable.GetRoomByReference("run1_1");
            if (room != null && room.roomMesh != null)
            {
                roomMesh = room.roomMesh;
                var go = Instantiate(roomMesh, Vector3.zero, Quaternion.identity, scansContainer.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.name = $"{roomMesh.name}_reference";
                // Set material to fully opaque
                go.gameObject.GetComponentInChildren<MeshRenderer>().material.color = new Color(
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.r,
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.g, 
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.b,
                    1);
                referenceObject = go;
                Debug.Log($"Reference object set to: {referenceObject}");
            }
            else
            {
                Debug.LogError($"Room or room mesh not found in RoomTable for reference. Please ensure RoomTable is assigned and contains the reference.");
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

        if (RoomTable != null) 
        {
            int i = 0;
            foreach (var scan in RoomTable.rooms) 
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

                    try
                    {
      
                        var go = Instantiate(scanRoomMesh, scansContainer.transform);
                        //
                        i++;
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.name = $"{scanRoomMesh.name}_{scan.reference}";
                        go.gameObject.GetComponentInChildren<MeshRenderer>().material.color = new Color(
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.r, 
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.g, 
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.b, 
                            0);
                        rescanObjects.Add(go);
                        go.SetActive(false);
                        //go.gameObject.SetActive(false); // Start inactive, will be activated via slider

                        if (enableDebugLogs) Debug.Log($"Instantiated rescan object local position: {go.transform.localPosition} | global position: {go.transform.position}");

                    }
                    catch (Exception e)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"Error processing scan {scan.reference}: {e.Message}");
                    }
            }
            mapViewerController.SetSliderProps(1f / RoomTable.rooms.Length+1);
        }
        
        // After all meshes are spawned, resize parent collider to fit all children
        ResizeParentCollider();
        
        // Initialize the MapViewerController with the spawned container
        if (mapViewerController != null)
        {
            mapViewerController.Initialize(scansContainer);
            if (enableDebugLogs) Debug.Log("[ProcessMapData] MapViewerController initialized");
        }
        else
        {
            Debug.LogWarning("[ProcessMapData] MapViewerController not assigned in Inspector");
        }
    }
#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator LoadMapDataFromStreamingAssets(string relativePath, string mapReference)
    {
        // Always reload RoomTable to ensure correct RoomTable for the current mapReference
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
        if(mapReference.Equals("flat")) {
            processFlatMap();
            yield break;
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
        
        // --- Sicherstellen, dass BoundingBoxes IMMER zum aktuellen Map geladen werden ---
        if (changeDetectionVisualizer != null && visualizeRemovedObjects)
        {
            changeDetectionVisualizer.ClearBoundingBoxes();
            if (enableDebugLogs) Debug.Log($"[ProcessMapData] Alte BoundingBoxes entfernt vor Laden von {mapReference}");
            changeDetectionVisualizer.LoadDatabases(mapReference, () => {
                if (enableDebugLogs) Debug.Log($"[ProcessMapData] Datenbank für BoundingBoxes mit mapReference {mapReference} geladen (Callback)");
                changeDetectionVisualizer.AnalyzeAmbiguity(mapData);
                if (enableDebugLogs) Debug.Log($"[ProcessMapData] Ambiguitätsanalyse für BoundingBoxes durchgeführt (Callback)");
            });
        }
        
        // Extract and log rigid transform data
        if (enableDebugLogs)
        {
            ExtractRigidTransforms(mapData);
        }
        
        // Destroy old scans container if it exists
        if (scansContainer != null)
        {
            Destroy(scansContainer);
            if (enableDebugLogs) Debug.Log("[ProcessMapData] Destroyed old ScansContainer");
        }
        
        scansContainer = new GameObject($"ScansContainer_{mapReference}");
        scansContainer.transform.position = spawnPosition;
        scansContainer.transform.rotation = Quaternion.Euler(globalRotation);
        scansContainer.transform.localScale = Vector3.one * spawnScale;
        
        // Add Rigidbody for physics-based interaction
        /*
        var rigidbody = scansContainer.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true; // kinematic so it doesn't fall
        rigidbody.useGravity = false;
        
        // Add BoxCollider for XR Grab detection (only one on parent)
        var collider = scansContainer.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.size = Vector3.one * 0.5f; // Default size, will be adjusted after mesh spawn
        
        // Add XRGrabInteractable for Meta XR controller grabbing
        var grabInteractable = scansContainer.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;*/
        
        
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
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.name = $"{roomMesh.name}_reference";
                // Set material to fully opaque
                go.gameObject.GetComponentInChildren<MeshRenderer>().material.color = new Color(
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.r,
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.g, 
                    go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.b,
                    1);
                referenceObject = go;
                Debug.Log($"Reference object set to: {referenceObject}");
                if (enableDebugLogs) Debug.Log($"Instantiated initial reference: {mapReference} at local position (0,0,0), global position: {go.transform.position}");
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
            int i = 0;
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
                        //
                        i++;
                        go.transform.localPosition = position;
                        go.transform.localRotation = rotation;
                        go.name = $"{scanRoomMesh.name}_{scan.reference}";
                        go.gameObject.GetComponentInChildren<MeshRenderer>().material.color = new Color(
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.r, 
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.g, 
                            go.gameObject.GetComponentInChildren<MeshRenderer>().material.color.b, 
                            0);
                        rescanObjects.Add(go);
                        //go.gameObject.SetActive(false); // Start inactive, will be activated via slider

                        if (enableDebugLogs) Debug.Log($"Instantiated rescan object local position: {go.transform.localPosition} | global position: {go.transform.position}");
                        
                        if (changeDetectionVisualizer != null && visualizeRemovedObjects)
                        {
                            // Visualize removed objects (red)
                            if (scan.removed != null && scan.removed.Count > 0)
                            {
                                if (enableDebugLogs) Debug.Log($"Visualizing {scan.removed.Count} removed objects for scan {scan.reference}");
                                changeDetectionVisualizer.VisualizeRemovedObjects(scan.removed, scansContainer.transform, scan.reference);
                            }
                            
                            // Visualize rigid objects (blue)
                            if (scan.rigid != null && scan.rigid.Count > 0)
                            {
                                if (enableDebugLogs) Debug.Log($"Visualizing {scan.rigid.Count} rigid objects for scan {scan.reference}");
                                changeDetectionVisualizer.VisualizeRigidObjects(scan.rigid, scansContainer.transform, scan.reference);
                            }
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
            mapViewerController.SetSliderProps(1f / mapData.scans.Count+2, changeDetectionVisualizer.boundingBoxContainer);
        }
        
        // After all meshes are spawned, resize parent collider to fit all children
        ResizeParentCollider();
        
        // Initialize the MapViewerController with the spawned container
        if (mapViewerController != null)
        {
            mapViewerController.Initialize(scansContainer);
            if (enableDebugLogs) Debug.Log("[ProcessMapData] MapViewerController initialized");
        }
        else
        {
            Debug.LogWarning("[ProcessMapData] MapViewerController not assigned in Inspector");
        }
    }
    
    /// <summary>
    /// Resizes the parent container's collider to fit all child meshes
    /// </summary>
    private void ResizeParentCollider()
    {
        var collider = scansContainer.GetComponent<BoxCollider>();
        if (collider == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[ResizeParentCollider] BoxCollider not found on container");
            return;
        }
        
        // Calculate combined bounds of all child renderers
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasRenderers = false;
        
        foreach (var renderer in scansContainer.GetComponentsInChildren<Renderer>())
        {
            if (!hasRenderers)
            {
                combinedBounds = renderer.bounds;
                hasRenderers = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }
        
        if (hasRenderers)
        {
            // Convert world bounds to local bounds
            Vector3 localCenter = scansContainer.transform.InverseTransformPoint(combinedBounds.center);
            Vector3 localSize = scansContainer.transform.InverseTransformVector(combinedBounds.size);
            
            collider.center = localCenter;
            collider.size = localSize * 1.1f; // 10% padding for easier grabbing
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ResizeParentCollider] Bounds center: {combinedBounds.center}, size: {combinedBounds.size}");
                Debug.Log($"[ResizeParentCollider] Collider center: {collider.center}, size: {collider.size}");
            }
        }
        else
        {
            if (enableDebugLogs) Debug.LogWarning("[ResizeParentCollider] No renderers found in container children");
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
    /// Extracts position and rotation from all rigid transforms in the map data
    /// </summary>
    /// <param name="mapData">The map data containing rigid transforms</param>
    public void ExtractRigidTransforms(MapData mapData)
    {
        if (mapData == null || mapData.scans == null)
        {
            Debug.LogWarning("[ExtractRigidTransforms] MapData or scans is null");
            return;
        }

        Debug.Log($"[ExtractRigidTransforms] Processing {mapData.scans.Count} scans");

        foreach (var scan in mapData.scans)
        {
            if (scan.rigid == null || scan.rigid.Count == 0)
            {
                Debug.Log($"[ExtractRigidTransforms] Scan '{scan.reference}' has no rigid transforms");
                continue;
            }

            Debug.Log($"[ExtractRigidTransforms] Scan '{scan.reference}' has {scan.rigid.Count} rigid transforms:");

            foreach (var rigidTransform in scan.rigid)
            {
                if (rigidTransform.transform == null || rigidTransform.transform.Length != 16)
                {
                    Debug.LogWarning($"[ExtractRigidTransforms] Invalid transform for instance {rigidTransform.instance_reference}");
                    continue;
                }

                // Convert float array to Matrix4x4
                Matrix4x4 matrix = GetMatrixFromFloatArray(rigidTransform.transform);

                // Extract position and rotation
                Vector3 position = matrix.GetPosition();
                Quaternion rotation = matrix.rotation;
                Vector3 eulerAngles = rotation.eulerAngles;

                // Log the data
                Debug.Log($"  Instance Reference: {rigidTransform.instance_reference} | Instance Rescan: {rigidTransform.instance_rescan}");
                Debug.Log($"    Position: ({position.x:F3}, {position.y:F3}, {position.z:F3})");
                Debug.Log($"    Rotation (Quaternion): ({rotation.x:F3}, {rotation.y:F3}, {rotation.z:F3}, {rotation.w:F3})");
                Debug.Log($"    Rotation (Euler): ({eulerAngles.x:F2}°, {eulerAngles.y:F2}°, {eulerAngles.z:F2}°)");
                Debug.Log($"    Symmetry: {rigidTransform.symmetry}");
            }
        }
    }

    /// <summary>
    /// Gets position and rotation for a specific instance reference
    /// </summary>
    /// <param name="mapData">The map data to search</param>
    /// <param name="instanceReference">The instance reference ID to find</param>
    /// <param name="position">Output position</param>
    /// <param name="rotation">Output rotation</param>
    /// <returns>True if found, false otherwise</returns>
    public bool GetRigidTransformForInstance(MapData mapData, int instanceReference, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (mapData == null || mapData.scans == null)
            return false;

        foreach (var scan in mapData.scans)
        {
            if (scan.rigid == null) continue;

            foreach (var rigidTransform in scan.rigid)
            {
                if (rigidTransform.instance_reference == instanceReference)
                {
                    if (rigidTransform.transform != null && rigidTransform.transform.Length == 16)
                    {
                        Matrix4x4 matrix = GetMatrixFromFloatArray(rigidTransform.transform);
                        position = matrix.GetPosition();
                        rotation = matrix.rotation;
                        return true;
                    }
                }
            }
        }

        return false;
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
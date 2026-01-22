using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;


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
    
    
    [Header("Map Viewer Controller")]
    public MapViewerController mapViewerController;
    
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

    }
    private void OnDisable()
    {

    }

    private void OnLoadMapAction(InputAction.CallbackContext context)
    {
        LoadSelectedMap(SceneHash);
    }
    /// <summary>
    /// Fully resets the scene: removes the scans container, rescan objects, and the reference object.
    /// </summary>
    public void ResetScene()
    {
        if (scansContainer != null)
        {
            Destroy(scansContainer);
            scansContainer = null;
            if (enableDebugLogs) Debug.Log("[ResetScene] ScansContainer entfernt.");
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
    }
#endif


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
            collider.size = localSize * 1.1f;
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

}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Data structures for semseg.v2.json
/// </summary>
[Serializable]
public class OBBData
{
    public float[] centroid;
    public float[] axesLengths;
    public float[] normalizedAxes;
}

[Serializable]
public class AABBData
{
    public float[] min;
    public float[] max;
}

[Serializable]
public class SegGroupData
{
    public int objectId;
    public int id;
    public string label;
    public OBBData obb;
    public AABBData aabb;
}

[Serializable]
public class SemanticSegmentation
{
    public List<SegGroupData> segGroups;
}

/// <summary>
/// CSV label data structure
/// </summary>
public class LabelData
{
    public int InstanceID;
    public int ClassID;
    public string Name;
    public int RIOGlobalID;
}

public class ChangeDetectionVisualizer : MonoBehaviour
{
    [Header("Paths")]
    public string semsegJsonPath = "semseg.v2.json"; // Deprecated - now using semseg_<HashID>.json
    public string csvPath = "groundtruth_labels.csv"; // Deprecated - now using groundtruth_<HashID>.csv
    
    [Header("Visualization")]
    public Material removedObjectMaterial;
    public Material movedObjectMaterial;
    public bool showBoundingBoxes = true;
    public bool useOBB = true;
    
    [Header("Debug Options")]
    public bool enableDebugLogs = true;
    
    private Dictionary<int, SegGroupData> segGroupDatabase = new Dictionary<int, SegGroupData>();
    private Dictionary<int, LabelData> labelDatabase = new Dictionary<int, LabelData>();
    private GameObject boundingBoxContainer;
    private Dictionary<string, GameObject> rescanContainers = new Dictionary<string, GameObject>();
    
    /// <summary>
    /// Loads semantic segmentation and label databases for a given scan reference
    /// </summary>
    public void LoadDatabases(string scanReference)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(LoadDatabasesAsync(scanReference));
#else
        LoadSemanticSegmentation(scanReference);
        LoadGroundTruthLabels();
#endif
    }
    
#if UNITY_ANDROID && !UNITY_EDITOR
    public IEnumerator LoadDatabasesAsync(string scanReference)
    {
        yield return StartCoroutine(LoadSemanticSegmentationAsync(scanReference));
        yield return StartCoroutine(LoadGroundTruthLabelsAsync());
    }
    
    public IEnumerator LoadSemanticSegmentationAsync(string scanReference)
    {
        string semsegFileName = $"semseg_{scanReference}.json";
        string fullPath = Path.Combine(Application.streamingAssetsPath, semsegFileName);
        
        using (UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError($"semseg_{scanReference}.json not found");
                    Debug.LogError($"Error: {www.error}");
                }
                yield break;
            }
            
            try
            {
                SemanticSegmentation semseg = JsonUtility.FromJson<SemanticSegmentation>(www.downloadHandler.text);
                
                segGroupDatabase.Clear();
                if (semseg.segGroups != null)
                {
                    foreach (var segGroup in semseg.segGroups)
                    {
                        segGroupDatabase[segGroup.objectId] = segGroup;
                    }
                }
                
                if (enableDebugLogs) Debug.Log($"Loaded {segGroupDatabase.Count} segGroups from {fullPath}");
            }
            catch (Exception e)
            {
                if (enableDebugLogs) Debug.LogError($"Error loading semseg.json: {e.Message}");
            }
        }
    }
    
    public IEnumerator LoadGroundTruthLabelsAsync()
    {
        string csvFileName = "groundtruth_labels.csv";
        string fullPath = Path.Combine(Application.streamingAssetsPath, csvFileName);
        
        using (UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                if (enableDebugLogs) Debug.LogWarning($"groundtruth_labels.csv not found");
                yield break;
            }
            
            try
            {
                string[] lines = www.downloadHandler.text.Split('\n');
                labelDatabase.Clear();
                
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    
                    string[] parts = lines[i].Split(',');
                    if (parts.Length >= 9)
                    {
                        LabelData label = new LabelData
                        {
                            InstanceID = int.Parse(parts[0]),
                            ClassID = int.Parse(parts[1]),
                            Name = parts[8],
                            RIOGlobalID = parts.Length > 9 ? int.Parse(parts[9]) : 0
                        };
                        labelDatabase[label.InstanceID] = label;
                    }
                }
                
                if (enableDebugLogs) Debug.Log($"Loaded {labelDatabase.Count} labels from {fullPath}");
            }
            catch (Exception e)
            {
                if (enableDebugLogs) Debug.LogError($"Error loading CSV: {e.Message}");
            }
        }
    }
#endif
    
    /// <summary>
    /// Loads semantic segmentation data from semseg_<HashID>.json for the specified scan
    /// </summary>
    private void LoadSemanticSegmentation(string scanReference)
    {
        // New naming scheme: semseg_<HashID>.json directly in StreamingAssets
        string semsegFileName = $"semseg_{scanReference}.json";
        string[] possiblePaths = new string[]
        {
            Path.Combine(Application.streamingAssetsPath, semsegFileName),
            Path.Combine(Application.dataPath, "..", "Downloads", "rio_subset", semsegFileName),
            semsegFileName
        };
        
        string foundPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                break;
            }
        }
        
        if (foundPath == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogError($"semseg_{scanReference}.json not found");
                Debug.LogError($"Tried: {string.Join(", ", possiblePaths)}");
            }
            return;
        }
        
        try
        {
            string jsonContent = File.ReadAllText(foundPath);
            SemanticSegmentation semseg = JsonUtility.FromJson<SemanticSegmentation>(jsonContent);
            
            segGroupDatabase.Clear();
            if (semseg.segGroups != null)
            {
                foreach (var segGroup in semseg.segGroups)
                {
                    segGroupDatabase[segGroup.objectId] = segGroup;
                }
            }
            
            if (enableDebugLogs) Debug.Log($"Loaded {segGroupDatabase.Count} segGroups from {foundPath}");
        }
        catch (Exception e)
        {
            if (enableDebugLogs) Debug.LogError($"Error loading semseg.json: {e.Message}");
        }
    }
    
    /// <summary>
    /// Loads ground truth labels from CSV file
    /// </summary>
    private void LoadGroundTruthLabels()
    {
        // groundtruth_labels.csv is shared across all scenes
        string csvFileName = "groundtruth_labels.csv";
        string[] possiblePaths = new string[]
        {
            Path.Combine(Application.streamingAssetsPath, csvFileName),
            Path.Combine(Application.dataPath, "..", "Downloads", "rio_subset", csvFileName),
            csvFileName
        };
        
        string foundPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                break;
            }
        }
        
        if (foundPath == null)
        {
            if (enableDebugLogs) Debug.LogWarning($"groundtruth_labels.csv not found");
            return;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(foundPath);
            labelDatabase.Clear();
            
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(',');
                if (parts.Length >= 9)
                {
                    LabelData label = new LabelData
                    {
                        InstanceID = int.Parse(parts[0]),
                        ClassID = int.Parse(parts[1]),
                        Name = parts[8],
                        RIOGlobalID = int.Parse(parts[9])
                    };
                    labelDatabase[label.InstanceID] = label;
                }
            }
            
            if (enableDebugLogs) Debug.Log($"Loaded {labelDatabase.Count} labels from {foundPath}");
        }
        catch (Exception e)
        {
            if (enableDebugLogs) Debug.LogError($"Error loading CSV: {e.Message}");
        }
    }
    
    /// <summary>
    /// Creates bounding box visualizations for removed objects
    /// </summary>
    /// <param name="removedIDs">List of object IDs that were removed</param>
    /// <param name="parentTransform">Parent transform to attach bounding boxes to</param>
    /// <param name="rescanHash">Hash ID of the rescan to organize bounding boxes</param>
    public void VisualizeRemovedObjects(List<int> removedIDs, Transform parentTransform, string rescanHash)
    {
        if (boundingBoxContainer == null)
        {
            boundingBoxContainer = new GameObject("RemovedObjects_BoundingBoxes");
            boundingBoxContainer.transform.SetParent(parentTransform, worldPositionStays: false);
            boundingBoxContainer.transform.localPosition = Vector3.zero;
            boundingBoxContainer.transform.localRotation = Quaternion.identity;
            boundingBoxContainer.transform.localScale = Vector3.one;
        }
        
        // Create or get container for this specific rescan
        GameObject rescanContainer;
        if (!rescanContainers.ContainsKey(rescanHash))
        {
            rescanContainer = new GameObject($"Rescan_{rescanHash}");
            rescanContainer.transform.SetParent(boundingBoxContainer.transform, worldPositionStays: false);
            rescanContainer.transform.localPosition = Vector3.zero;
            rescanContainer.transform.localRotation = Quaternion.identity;
            rescanContainer.transform.localScale = Vector3.one;
            rescanContainers[rescanHash] = rescanContainer;
            if (enableDebugLogs) Debug.Log($"Created container for rescan: {rescanHash}");
        }
        else
        {
            rescanContainer = rescanContainers[rescanHash];
        }
        
        if (enableDebugLogs) Debug.Log($"Removed Object IDs for rescan {rescanHash}: [{string.Join(", ", removedIDs)}]");
        
        foreach (int objectID in removedIDs)
        {
            if (!segGroupDatabase.ContainsKey(objectID))
            {
                if (enableDebugLogs) Debug.LogWarning($"Object ID {objectID} not found in semseg.json (available IDs: {string.Join(", ", segGroupDatabase.Keys)});");
                continue;
            }
            
            SegGroupData segGroup = segGroupDatabase[objectID];
            
            string objectName = !string.IsNullOrEmpty(segGroup.label) ? segGroup.label : $"Object_{objectID}";
            
            if (enableDebugLogs) Debug.Log($"Creating bounding box for Object ID {objectID}: {objectName} in rescan {rescanHash}");
            
            GameObject bbox = CreateBoundingBox(segGroup, objectName);
            bbox.transform.SetParent(rescanContainer.transform, worldPositionStays: false);
            
            if (enableDebugLogs) Debug.Log($"Removed object visualized: {objectName} (ID: {objectID}) at position: {bbox.transform.position}");
        }
    }

    /// <summary>
    /// Creates bounding box visualizations for rigid (moved/unchanged) objects
    /// </summary>
    /// <param name="rigidTransforms">List of rigid transforms from the scan data</param>
    /// <param name="parentTransform">Parent transform to attach bounding boxes to</param>
    /// <param name="rescanHash">Hash ID of the rescan to organize bounding boxes</param>
    public void VisualizeRigidObjects(List<RigidTransform> rigidTransforms, Transform parentTransform, string rescanHash)
    {
        if (rigidTransforms == null || rigidTransforms.Count == 0)
        {
            if (enableDebugLogs) Debug.Log($"No rigid objects to visualize for rescan {rescanHash}");
            return;
        }

        if (boundingBoxContainer == null)
        {
            boundingBoxContainer = new GameObject("RemovedObjects_BoundingBoxes");
            boundingBoxContainer.transform.SetParent(parentTransform, worldPositionStays: false);
            boundingBoxContainer.transform.localPosition = Vector3.zero;
            boundingBoxContainer.transform.localRotation = Quaternion.identity;
            boundingBoxContainer.transform.localScale = Vector3.one;
        }
        
        // Create or get container for rigid objects
        GameObject rigidContainer;
        string rigidContainerKey = $"{rescanHash}_rigid";
        if (!rescanContainers.ContainsKey(rigidContainerKey))
        {
            rigidContainer = new GameObject($"Rigid_{rescanHash}");
            rigidContainer.transform.SetParent(boundingBoxContainer.transform, worldPositionStays: false);
            rigidContainer.transform.localPosition = Vector3.zero;
            rigidContainer.transform.localRotation = Quaternion.identity;
            rigidContainer.transform.localScale = Vector3.one;
            rescanContainers[rigidContainerKey] = rigidContainer;
            if (enableDebugLogs) Debug.Log($"Created rigid container for rescan: {rescanHash}");
        }
        else
        {
            rigidContainer = rescanContainers[rigidContainerKey];
        }
        
        if (enableDebugLogs) Debug.Log($"Visualizing {rigidTransforms.Count} rigid objects for rescan {rescanHash}");
        
        foreach (var rigidTransform in rigidTransforms)
        {
            int objectID = rigidTransform.instance_reference;
            
            if (!segGroupDatabase.ContainsKey(objectID))
            {
                if (enableDebugLogs) Debug.LogWarning($"Rigid object ID {objectID} not found in semseg.json");
                continue;
            }
            
            SegGroupData segGroup = segGroupDatabase[objectID];
            string objectName = !string.IsNullOrEmpty(segGroup.label) ? segGroup.label : $"Object_{objectID}";
            
            if (enableDebugLogs) Debug.Log($"Creating bounding box for rigid Object ID {objectID}: {objectName}");
            
            // Create blue bounding box for rigid objects
            GameObject bbox = CreateBoundingBox(segGroup, objectName, prefix: "Moved");
            bbox.transform.SetParent(rigidContainer.transform, worldPositionStays: false);
            
            if (enableDebugLogs) Debug.Log($"Rigid object visualized: {objectName} (ID: {objectID}) at position: {bbox.transform.position}");
        }
    }

    public void VisualizeMovedObjects(List<int> movedIDs, Transform parentTransform, string rescanHash)
    {
        // Implementation for visualizing moved objects can be added here
    }
    
    /// <summary>
    /// Creates a bounding box GameObject for a removed object
    /// </summary>
    /// <param name="segGroup">Semantic segmentation group data containing OBB/AABB information</param>
    /// <param name="name">Name for the bounding box object</param>
    /// <param name="color">Optional custom color for the bounding box (default: red)</param>
    /// <param name="prefix">Prefix for the GameObject name (default: Removed)</param>
    /// <returns>The created bounding box GameObject</returns>
    private GameObject CreateBoundingBox(SegGroupData segGroup, string name, Color? color = null, string prefix = "Removed")
    {
        GameObject bbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bbox.name = $"{prefix}_{name}";
        
        Destroy(bbox.GetComponent<Collider>());
        
        Color boxColor = color ?? new Color(1, 0, 0, 0.1f);
        
        if (removedObjectMaterial != null && !color.HasValue)
        {
            if(prefix == "Moved")
                bbox.GetComponent<Renderer>().material = movedObjectMaterial;
            else
                bbox.GetComponent<Renderer>().material = removedObjectMaterial;
        }
        else
        {
            //Create a semi-transparent material from scratch
            Material mat = bbox.GetComponent<Renderer>().material;
            mat.color = boxColor;
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        
        if (useOBB && segGroup.obb != null && segGroup.obb.centroid != null && segGroup.obb.centroid.Length == 3)
        {
            Vector3 center = new Vector3(
                -segGroup.obb.centroid[0],
                segGroup.obb.centroid[1],
                segGroup.obb.centroid[2]
            );
            
            Vector3 dimensions = new Vector3(
                segGroup.obb.axesLengths[0],
                segGroup.obb.axesLengths[1],
                segGroup.obb.axesLengths[2]
            );
            
            bbox.transform.localPosition = center;
            bbox.transform.localScale = dimensions;
            
            if (segGroup.obb.normalizedAxes != null && segGroup.obb.normalizedAxes.Length == 9)
            {
                float[] axes = segGroup.obb.normalizedAxes;
                
                Matrix4x4 rotationMatrix = new Matrix4x4();
                rotationMatrix.m00 = axes[0];  rotationMatrix.m01 = axes[1];  rotationMatrix.m02 = axes[2];  rotationMatrix.m03 = 0;
                rotationMatrix.m10 = axes[3];  rotationMatrix.m11 = axes[4];  rotationMatrix.m12 = axes[5];  rotationMatrix.m13 = 0;
                rotationMatrix.m20 = axes[6];  rotationMatrix.m21 = axes[7];  rotationMatrix.m22 = axes[8];  rotationMatrix.m23 = 0;
                rotationMatrix.m30 = 0;        rotationMatrix.m31 = 0;        rotationMatrix.m32 = 0;        rotationMatrix.m33 = 1;
                
                rotationMatrix = rotationMatrix.transpose;
                
                Quaternion rotation = rotationMatrix.rotation;
                Vector3 eulerAngles = rotation.eulerAngles;
                eulerAngles.z = -eulerAngles.z;
                eulerAngles.x = -eulerAngles.x;
                rotation = Quaternion.Euler(eulerAngles);
                
                bbox.transform.localRotation = rotation;
            }
        }
        else if (segGroup.aabb != null && segGroup.aabb.min != null && segGroup.aabb.max != null)
        {
            Vector3 min = new Vector3(-segGroup.aabb.min[0], segGroup.aabb.min[1], segGroup.aabb.min[2]);
            Vector3 max = new Vector3(-segGroup.aabb.max[0], segGroup.aabb.max[1], segGroup.aabb.max[2]);
            
            Vector3 center = (min + max) / 2f;
            Vector3 dimensions = max - min;
            
            bbox.transform.localPosition = center;
            bbox.transform.localScale = dimensions;
        }
        else
        {
            if (enableDebugLogs) Debug.LogWarning($"No bounding box data for {name}");
            bbox.transform.localPosition = Vector3.zero;
            bbox.transform.localScale = Vector3.one * 0.1f;
        }
        
        return bbox;
    }
    
    /// <summary>
    /// Clears all bounding box visualizations
    /// </summary>
    public void ClearBoundingBoxes()
    {
        if (boundingBoxContainer != null)
        {
            Destroy(boundingBoxContainer);
            boundingBoxContainer = null;
        }
        rescanContainers.Clear();
    }
    
    /// <summary>
    /// Clears bounding boxes for a specific rescan
    /// </summary>
    /// <param name="rescanHash">Hash ID of the rescan to clear</param>
    public void ClearRescanBoundingBoxes(string rescanHash)
    {
        if (rescanContainers.ContainsKey(rescanHash))
        {
            Destroy(rescanContainers[rescanHash]);
            rescanContainers.Remove(rescanHash);
            if (enableDebugLogs) Debug.Log($"Cleared bounding boxes for rescan: {rescanHash}");
        }
    }

    /// <summary>
    /// Analyzes ambiguity groups from MapData to find potential mislabels (e.g., table vs chair)
    /// Heuristik:
    ///  - Ein "table" sollte mindestens eine größere Fläche haben (maxAchse >= 0.8m oder Fläche der zwei größten Achsen >= 0.4 m^2)
    ///  - "chair" ist typischerweise kleiner (maxAchse < 0.8m und Volumen < 0.5 m^3)
    ///  Verdächtig: Label == table aber zu klein, oder Label == chair aber sehr groß
    /// </summary>
    /// <param name="mapData">Geladene MapData mit ambiguity Gruppen</param>
    /// <param name="visualizeSuspicious">Erstellt gelbe Bounding Box für verdächtige Objekte</param>
    public void AnalyzeAmbiguity(MapData mapData, bool visualizeSuspicious = true)
    {
        if (mapData == null || mapData.ambiguity == null || mapData.ambiguity.Count == 0)
        {
            if (enableDebugLogs) Debug.Log("[AmbiguityAnalysis] Keine Ambiguity Daten vorhanden");
            return;
        }

        HashSet<int> involvedInstanceIds = new HashSet<int>();
        foreach (var group in mapData.ambiguity)
        {
            if (group == null) continue;
            foreach (var t in group)
            {
                involvedInstanceIds.Add(t.instance_source);
                involvedInstanceIds.Add(t.instance_target);
            }
        }

        if (enableDebugLogs) Debug.Log($"[AmbiguityAnalysis] Prüfe {involvedInstanceIds.Count} Instanzen aus Ambiguity-Gruppen gegen semseg Daten");

        List<(int id, string label, Vector3 size, float volume, string reason)> suspicious = new List<(int, string, Vector3, float, string)>();

        foreach (int id in involvedInstanceIds)
        {
            if (!segGroupDatabase.TryGetValue(id, out var seg))
            {
                if (enableDebugLogs) Debug.LogWarning($"[AmbiguityAnalysis] Instanz {id} nicht in semseg gefunden");
                continue;
            }

            // Bestimme Größe aus OBB oder AABB
            Vector3 size = Vector3.one * -1f;
            if (useOBB && seg.obb != null && seg.obb.axesLengths != null && seg.obb.axesLengths.Length == 3)
            {
                size = new Vector3(seg.obb.axesLengths[0], seg.obb.axesLengths[1], seg.obb.axesLengths[2]);
            }
            else if (seg.aabb != null && seg.aabb.min != null && seg.aabb.max != null)
            {
                Vector3 min = new Vector3(seg.aabb.min[0], seg.aabb.min[1], seg.aabb.min[2]);
                Vector3 max = new Vector3(seg.aabb.max[0], seg.aabb.max[1], seg.aabb.max[2]);
                size = max - min;
            }

            if (size.x < 0) // Keine gültige Größe
            {
                if (enableDebugLogs) Debug.LogWarning($"[AmbiguityAnalysis] Keine Größeninfo für Instanz {id} (Label: {seg.label})");
                continue;
            }

            float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            // Fläche der zwei größten Achsen für Tischplatte-Approx.
            float[] axesArr = new float[] { size.x, size.y, size.z };
            Array.Sort(axesArr); // aufsteigend
            float topArea = axesArr[2] * axesArr[1];
            float volume = size.x * size.y * size.z;
            string labelLower = (seg.label ?? "").ToLowerInvariant();

            bool flagged = false;
            string reason = string.Empty;

            if (labelLower.Contains("table"))
            {
                // Zu klein für Tisch
                if (maxAxis < 0.8f || topArea < 0.4f)
                {
                    flagged = true;
                    reason = $"Label table wirkt zu klein (maxAxis={maxAxis:F2}, topArea={topArea:F2})";
                }
            }
            else if (labelLower.Contains("chair"))
            {
                // Ungewöhnlich groß für Stuhl
                if (maxAxis > 1.2f || volume > 0.8f)
                {
                    flagged = true;
                    reason = $"Label chair wirkt zu groß (maxAxis={maxAxis:F2}, volume={volume:F2})";
                }
            }

            if (flagged)
            {
                suspicious.Add((id, seg.label, size, volume, reason));
                Debug.LogWarning($"[AmbiguityAnalysis] Mögliche Fehlklassifizierung: ID={id} Label={seg.label} Größe=({size.x:F2},{size.y:F2},{size.z:F2}) Volumen={volume:F2} -> {reason}");

                if (visualizeSuspicious)
                {
                    // Erzeuge gelbe Bounding Box
                    var bbox = CreateBoundingBox(seg, seg.label, new Color(1f, 0.9f, 0.1f, 0.35f), prefix: "Suspect");
                    // Container sicherstellen
                    if (boundingBoxContainer == null)
                    {
                        boundingBoxContainer = new GameObject("RemovedObjects_BoundingBoxes");
                        bbox.transform.SetParent(boundingBoxContainer.transform);
                    }
                    else
                    {
                        bbox.transform.SetParent(boundingBoxContainer.transform);
                    }
                }
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[AmbiguityAnalysis] Analyse abgeschlossen. Verdächtige Objekte: {suspicious.Count}");
        }
    }
}

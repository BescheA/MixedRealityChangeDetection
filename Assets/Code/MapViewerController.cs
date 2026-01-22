using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles UI-based manipulation of the loaded map visualization
/// Supports rotation and scaling via UI buttons/toggles
/// </summary>
public class MapViewerController : MonoBehaviour
{
    [Header("Manipulation Settings")]
    public float rotationSpeed = 45f; // degrees per second
    public float minScale = 0.05f;
    public float maxScale = 0.2f;
    public float scaleSpeed = 0.1f; // scale units per second
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    private GameObject mapContainer;
    
    // Toggle/button states for continuous actions
    private bool rotatingUp = false;
    private bool rotatingDown = false;
    private bool rotatingLeft = false;
    private bool rotatingRight = false;
    private bool scalingUp = false;
    private bool scalingDown = false;

    private float sliderValue = 0f;
    private float steps = 0f;

    [Header("UI References")]
    public Slider timestampSlider;

    [Header("Manager References")]

    public loadMap mapLoader;

    /// <summary>
    /// Initialize the controller with a map container
    /// Call this after the map is spawned
    /// </summary>
    public void Initialize(GameObject container)
    {
        mapContainer = container;
        if (enableDebugLogs) Debug.Log($"[MapViewerController] Initialized with container: {container.name}");
        mapLoader = FindFirstObjectByType<loadMap>();
        timestampSlider.value = 0;
        sliderValue = timestampSlider.value;
    }

    void Update()
    {
        // Only process if container exists
        if (mapContainer == null) return;

        // Calculate the geometric center of all child renderers
        Renderer[] renderers = mapContainer.GetComponentsInChildren<Renderer>();
        Vector3 rotationCenter = mapContainer.transform.position;
        
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            rotationCenter = bounds.center;
        }

        // Apply rotations based on active states (rotate around geometric center)
        if (rotatingUp)
        {
            mapContainer.transform.RotateAround(rotationCenter, mapContainer.transform.right, -rotationSpeed * Time.deltaTime);
        }
        if (rotatingDown)
        {
            mapContainer.transform.RotateAround(rotationCenter, mapContainer.transform.right, rotationSpeed * Time.deltaTime);
        }
        if (rotatingLeft)
        {
            mapContainer.transform.RotateAround(rotationCenter, Vector3.up, -rotationSpeed * Time.deltaTime);
        }
        if (rotatingRight)
        {
            mapContainer.transform.RotateAround(rotationCenter, Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // Apply scaling based on active states
        if (scalingUp)
        {
            Vector3 newScale = mapContainer.transform.localScale + Vector3.one * scaleSpeed * Time.deltaTime;
            newScale = Vector3.one * Mathf.Clamp(newScale.x, minScale, maxScale);
            mapContainer.transform.localScale = newScale;
        }
        if (scalingDown)
        {
            Vector3 newScale = mapContainer.transform.localScale - Vector3.one * scaleSpeed * Time.deltaTime;
            newScale = Vector3.one * Mathf.Clamp(newScale.x, minScale, maxScale);
            mapContainer.transform.localScale = newScale;
        }
    }

    #region Rotation Controls - Toggle Based

    /// <summary>
    /// Start rotating up (around X-axis, negative direction)
    /// Call on Toggle/Button press and hold
    /// </summary>
    public void StartRotateUp()
    {
        rotatingUp = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Rotate Up");
    }

    /// <summary>
    /// Stop rotating up
    /// Call on Toggle/Button release
    /// </summary>
    public void StopRotateUp()
    {
        rotatingUp = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Rotate Up");
    }

    /// <summary>
    /// Start rotating down (around X-axis, positive direction)
    /// </summary>
    public void StartRotateDown()
    {
        rotatingDown = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Rotate Down");
    }

    /// <summary>
    /// Stop rotating down
    /// </summary>
    public void StopRotateDown()
    {
        rotatingDown = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Rotate Down");
    }

    /// <summary>
    /// Start rotating left (around Y-axis, negative direction)
    /// </summary>
    public void StartRotateLeft()
    {
        rotatingLeft = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Rotate Left");
    }

    /// <summary>
    /// Stop rotating left
    /// </summary>
    public void StopRotateLeft()
    {
        rotatingLeft = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Rotate Left");
    }

    /// <summary>
    /// Start rotating right (around Y-axis, positive direction)
    /// </summary>
    public void StartRotateRight()
    {
        rotatingRight = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Rotate Right");
    }

    /// <summary>
    /// Stop rotating right
    /// </summary>
    public void StopRotateRight()
    {
        rotatingRight = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Rotate Right");
    }

    #endregion

    #region Scale Controls - Toggle Based

    /// <summary>
    /// Start scaling up (increase size)
    /// Call on Toggle/Button press and hold
    /// </summary>
    public void StartScaleUp()
    {
        scalingUp = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Scale Up");
    }

    /// <summary>
    /// Stop scaling up
    /// Call on Toggle/Button release
    /// </summary>
    public void StopScaleUp()
    {
        scalingUp = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Scale Up");
    }

    /// <summary>
    /// Start scaling down (decrease size)
    /// </summary>
    public void StartScaleDown()
    {
        scalingDown = true;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Start Scale Down");
    }

    /// <summary>
    /// Stop scaling down
    /// </summary>
    public void StopScaleDown()
    {
        scalingDown = false;
        if (enableDebugLogs) Debug.Log("[MapViewerController] Stop Scale Down");
    }

    public void OnSliderChange()
    {
        sliderValue = timestampSlider.value;
        
        // Update mesh visibility and transparency based on slider value
        if (mapLoader != null)
        {
            UpdateMeshFading(sliderValue);
        }
    }

    /// <summary>
    /// Updates mesh transparency and visibility based on slider position
    /// Smoothly crossfades between consecutive meshes: reference (0) → rescan1 (0.25) → rescan2 (0.5) etc
    /// Each mesh occupies exactly one segment of the slider range
    /// </summary>
    private void UpdateMeshFading(float sliderValue)
    {
        GameObject referenceObject = mapLoader.referenceObject;
        List<GameObject> rescanObjects = mapLoader.rescanObjects;

        if (referenceObject == null || rescanObjects == null || rescanObjects.Count == 0)
        {
            if (enableDebugLogs) Debug.LogWarning("[MapViewerController] Reference or rescan objects not available");
            return;
        }

        // Build ordered list: index 0 = reference, indices 1..N = rescans
        List<GameObject> allMeshes = new List<GameObject>(rescanObjects.Count + 1);
        allMeshes.Add(referenceObject);
        allMeshes.AddRange(rescanObjects);

        int totalMeshes = allMeshes.Count;
        float segmentSize = 1f / totalMeshes;

        // Determine which mesh pair is currently transitioning
        int prevIndex = Mathf.Clamp(Mathf.FloorToInt(sliderValue / segmentSize), 0, totalMeshes - 1);
        int nextIndex = Mathf.Min(prevIndex + 1, totalMeshes - 1);
        
        // Blend within current segment: 0 = fully prev mesh, 1 = fully next mesh
        float segmentStart = prevIndex * segmentSize;
        float blend = (sliderValue - segmentStart) / segmentSize;
        blend = Mathf.Clamp01(blend);

        // Use a threshold for hard-switching instead of transparency to avoid rendering issues
        const float switchThreshold = 0.5f;

        // Update all meshes - use hard switching to avoid transparency issues
        for (int i = 0; i < allMeshes.Count; i++)
        {
            GameObject mesh = allMeshes[i];
            if (mesh == null) continue;

            bool isActive = false;

            if (i == prevIndex && i == nextIndex)
            {
                // Single mesh in its segment (at slider endpoint)
                isActive = true;
            }
            else if (i == prevIndex)
            {
                // Previous mesh visible until threshold
                isActive = blend < switchThreshold;
            }
            else if (i == nextIndex)
            {
                // Next mesh visible after threshold
                isActive = blend >= switchThreshold;
            }
            else
            {
                // All other meshes are hidden
                isActive = false;
            }

            mesh.SetActive(isActive);
            
            // Keep materials fully opaque to avoid rendering issues
            if (isActive)
            {
                SetMeshFullyOpaque(mesh);
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[MapViewerController] Slider: {sliderValue:F2}, Prev: {prevIndex} (active: {blend < switchThreshold}), Next: {nextIndex} (active: {blend >= switchThreshold}), Blend: {blend:F2}");
        }
    }

    /// <summary>
    /// Ensures a mesh is fully opaque without any transparency
    /// </summary>
    private void SetMeshFullyOpaque(GameObject meshObject)
    {
        if (meshObject == null) return;

        Renderer[] renderers = meshObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer.material.HasProperty("_Color"))
            {
                Color color = renderer.material.color;
                color.a = 1f;
                renderer.material.color = color;
            }
            
            // Ensure opaque rendering mode
            if (renderer.material.HasProperty("_Surface"))
            {
                renderer.material.SetFloat("_Surface", 0f); // Opaque
            }
            
            renderer.material.renderQueue = 2000; // Opaque queue
        }
    }

    public void SetSliderProps(float steps)
    {
        Debug.Log("Setting slider properties");
        timestampSlider.minValue = 0;
        timestampSlider.maxValue = 1;
        timestampSlider.wholeNumbers = false;
        timestampSlider.value = 0;
        this.steps = steps; // Slider clamped between 0 and 1
    }

    #endregion
}

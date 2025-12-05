using UnityEngine;

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

    /// <summary>
    /// Initialize the controller with a map container
    /// Call this after the map is spawned
    /// </summary>
    public void Initialize(GameObject container)
    {
        mapContainer = container;
        if (enableDebugLogs) Debug.Log($"[MapViewerController] Initialized with container: {container.name}");
    }

    void Update()
    {
        // Only process if container exists
        if (mapContainer == null) return;

        // Apply rotations based on active states
        if (rotatingUp)
        {
            mapContainer.transform.Rotate(-rotationSpeed * Time.deltaTime, 0, 0, Space.Self);
        }
        if (rotatingDown)
        {
            mapContainer.transform.Rotate(rotationSpeed * Time.deltaTime, 0, 0, Space.Self);
        }
        if (rotatingLeft)
        {
            mapContainer.transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0, Space.World);
        }
        if (rotatingRight)
        {
            mapContainer.transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
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

    #endregion
}

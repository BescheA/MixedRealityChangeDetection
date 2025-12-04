using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

/// <summary>
/// Allows interactive manipulation of the scans container through grab, move, rotate, and scale operations
/// Supports both direct grab and ray-based point-and-grab interaction with visual feedback
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class ScansContainerManipulation : MonoBehaviour
{
    [Header("Manipulation Settings")]
    [Tooltip("Speed of rotation when using controllers")]
    public float rotationSpeed = 50f;
    
    [Tooltip("Speed of scaling")]
    public float scaleSpeed = 0.5f;
    
    [Tooltip("Minimum uniform scale")]
    public float minScale = 0.1f;
    
    [Tooltip("Maximum uniform scale")]
    public float maxScale = 10f;
    
    [Header("Collider Settings")]
    [Tooltip("Padding added to collider bounds for easier grabbing (in meters)")]
    public float colliderPadding = 0.3f;
    
    [Tooltip("Use multiple sphere colliders instead of box for better inside-object detection")]
    public bool useMultipleSphereColliders = true;
    
    [Header("Visual Feedback")]
    [Tooltip("Enable visual feedback when hovering")]
    public bool enableHoverFeedback = true;
    
    [Tooltip("Color tint when hovering over object")]
    public Color hoverColor = new Color(0.3f, 0.7f, 1f, 0.3f);
    
    [Tooltip("Highlight intensity (0-1)")]
    [Range(0f, 1f)]
    public float hoverIntensity = 0.3f;
    
    [Header("Input Actions")]
    [Tooltip("Right thumbstick for rotation in all directions")]
    public InputActionReference rightThumbstickAction;
    
    [Tooltip("Left thumbstick for scaling (up/down)")]
    public InputActionReference leftThumbstickAction;
    
    [Header("Debug")]
    public bool enableDebugLogs = false;
    
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Vector3 initialScale;
    private bool isGrabbed = false;
    private bool isHovered = false;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private GameObject hoverIndicator;
    
    void Awake()
    {
        SetupPhysics();
        SetupColliders();
        SetupGrabInteractable();
        SetupVisualFeedback();
        LoadInputActionsIfNeeded();
        RegisterEvents();
        
        initialScale = transform.localScale;
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Initialized for {gameObject.name}");
    }
    
    private void SetupPhysics()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
    
    private void SetupColliders()
    {
        // Remove existing colliders to rebuild
        foreach (var col in GetComponents<Collider>())
        {
            Destroy(col);
        }
        
        // Calculate combined bounds
        Bounds combinedBounds = CalculateCombinedBounds();
        
        if (useMultipleSphereColliders)
        {
            // Create multiple sphere colliders for better coverage, especially when inside
            CreateMultipleSphereColliders(combinedBounds);
        }
        else
        {
            // Create single box collider
            CreateBoxCollider(combinedBounds);
        }
    }
    
    private Bounds CalculateCombinedBounds()
    {
        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in childRenderers)
        {
            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }
        
        if (!hasBounds)
        {
            Debug.LogWarning("[ScansManipulation] No child meshes found for bounds calculation");
            combinedBounds = new Bounds(transform.position, Vector3.one);
        }
        
        return combinedBounds;
    }
    
    private void CreateBoxCollider(Bounds bounds)
    {
        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        
        // Convert world bounds to local space and add padding
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        
        boxCollider.center = localCenter;
        boxCollider.size = localSize + Vector3.one * colliderPadding;
        
        if (enableDebugLogs)
            Debug.Log($"[ScansManipulation] Created BoxCollider - Size: {boxCollider.size}, Center: {boxCollider.center}");
    }
    
    private void CreateMultipleSphereColliders(Bounds bounds)
    {
        // Create grid of sphere colliders for better coverage
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        
        // Determine number of spheres based on size
        int sphereCount = 3; // 3x3x3 grid
        float spacing = Mathf.Max(localSize.x, localSize.y, localSize.z) / (sphereCount - 1);
        float radius = spacing * 0.6f + colliderPadding;
        
        int totalSpheres = 0;
        for (int x = 0; x < sphereCount; x++)
        {
            for (int y = 0; y < sphereCount; y++)
            {
                for (int z = 0; z < sphereCount; z++)
                {
                    Vector3 offset = new Vector3(
                        (x - (sphereCount - 1) / 2f) * spacing,
                        (y - (sphereCount - 1) / 2f) * spacing,
                        (z - (sphereCount - 1) / 2f) * spacing
                    );
                    
                    SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                    sphere.center = localCenter + offset;
                    sphere.radius = radius;
                    totalSpheres++;
                }
            }
        }
        
        if (enableDebugLogs)
            Debug.Log($"[ScansManipulation] Created {totalSpheres} SphereColliders with radius: {radius}");
    }
    
    private void SetupGrabInteractable()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }
        
        // Configure for both direct and ray interaction
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        grabInteractable.throwOnDetach = false;
        grabInteractable.retainTransformParent = true;
        
        // Enable both select modes for grab flexibility
        grabInteractable.selectMode = InteractableSelectMode.Multiple;
        grabInteractable.interactionLayers = InteractionLayerMask.GetMask("Default");
    }
    
    private void SetupVisualFeedback()
    {
        if (!enableHoverFeedback) return;
        
        // Create hover indicator (semi-transparent overlay)
        hoverIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hoverIndicator.name = "HoverIndicator";
        hoverIndicator.transform.SetParent(transform);
        hoverIndicator.transform.localPosition = Vector3.zero;
        hoverIndicator.transform.localRotation = Quaternion.identity;
        
        // Remove collider from indicator
        Destroy(hoverIndicator.GetComponent<Collider>());
        
        // Setup material
        var renderer = hoverIndicator.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        renderer.material.color = hoverColor;
        renderer.material.SetFloat("_Surface", 1); // Transparent
        renderer.material.SetFloat("_Blend", 0); // Alpha
        
        // Match bounds
        Bounds bounds = CalculateCombinedBounds();
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        hoverIndicator.transform.localScale = localSize * 1.05f; // Slightly larger
        
        hoverIndicator.SetActive(false);
    }
    
    private void LoadInputActionsIfNeeded()
    {
        if (rightThumbstickAction != null && leftThumbstickAction != null) return;
        
        var inputActions = Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>();
        
        foreach (var asset in inputActions)
        {
            if (asset.name.Contains("XRI") || asset.name.Contains("Default Input Actions"))
            {
                if (rightThumbstickAction == null)
                {
                    var rightAction = asset.FindAction("XRI RightHand Locomotion/Primary 2D Axis");
                    if (rightAction == null) rightAction = asset.FindAction("XRI RightHand/Move");
                    if (rightAction != null)
                    {
                        rightThumbstickAction = InputActionReference.Create(rightAction);
                        if (enableDebugLogs) Debug.Log("[ScansManipulation] Auto-assigned right thumbstick");
                    }
                }
                
                if (leftThumbstickAction == null)
                {
                    var leftAction = asset.FindAction("XRI LeftHand Locomotion/Primary 2D Axis");
                    if (leftAction == null) leftAction = asset.FindAction("XRI LeftHand/Move");
                    if (leftAction != null)
                    {
                        leftThumbstickAction = InputActionReference.Create(leftAction);
                        if (enableDebugLogs) Debug.Log("[ScansManipulation] Auto-assigned left thumbstick");
                    }
                }
                
                if (rightThumbstickAction != null && leftThumbstickAction != null) break;
            }
        }
    }
    
    private void RegisterEvents()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
    }
    
    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            grabInteractable.hoverExited.RemoveListener(OnHoverExited);
        }
    }
    
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        if (hoverIndicator != null) hoverIndicator.SetActive(false);
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Grabbed by {args.interactorObject}");
    }
    
    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        if (enableDebugLogs) Debug.Log("[ScansManipulation] Released");
    }
    
    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        if (enableHoverFeedback && hoverIndicator != null && !isGrabbed)
        {
            hoverIndicator.SetActive(true);
        }
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Hover entered by {args.interactorObject}");
    }
    
    private void OnHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
        if (hoverIndicator != null)
        {
            hoverIndicator.SetActive(false);
        }
        if (enableDebugLogs) Debug.Log("[ScansManipulation] Hover exited");
    }
    
    void Update()
    {
        if (!isGrabbed) return;
        
        HandleRotation();
        HandleScaling();
    }
    
    private void HandleRotation()
    {
        if (rightThumbstickAction == null || rightThumbstickAction.action == null) return;
        
        Vector2 thumbstick = rightThumbstickAction.action.ReadValue<Vector2>();
        
        if (Mathf.Abs(thumbstick.x) > 0.1f)
        {
            float rotationY = thumbstick.x * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationY, Space.World);
        }
        
        if (Mathf.Abs(thumbstick.y) > 0.1f)
        {
            float rotationX = thumbstick.y * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.right, rotationX, Space.Self);
        }
    }
    
    private void HandleScaling()
    {
        if (leftThumbstickAction == null || leftThumbstickAction.action == null) return;
        
        Vector2 leftStick = leftThumbstickAction.action.ReadValue<Vector2>();
        
        if (Mathf.Abs(leftStick.y) > 0.1f)
        {
            float scaleChange = leftStick.y * scaleSpeed * Time.deltaTime;
            Vector3 newScale = transform.localScale + Vector3.one * scaleChange;
            newScale = Vector3.one * Mathf.Clamp(newScale.x, minScale, maxScale);
            transform.localScale = newScale;
        }
    }
    
    /// <summary>
    /// Reset scale to initial value
    /// </summary>
    public void ResetScale()
    {
        transform.localScale = initialScale;
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Reset scale to {initialScale}");
    }
    
    /// <summary>
    /// Reset position and rotation
    /// </summary>
    public void ResetTransform()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        if (enableDebugLogs) Debug.Log("[ScansManipulation] Reset transform");
    }
    
    /// <summary>
    /// Reset everything to initial state
    /// </summary>
    public void ResetAll()
    {
        ResetScale();
        ResetTransform();
    }
}

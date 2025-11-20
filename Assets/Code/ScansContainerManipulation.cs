using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Allows interactive manipulation of the scans container through grab, move, rotate, and scale operations
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
    
    [Header("Input Actions")]
    [Tooltip("Left thumbstick for rotation around Y-axis")]
    public bool enableThumbstickRotation = true;
    
    [Tooltip("Trigger buttons for scaling")]
    public bool enableTriggerScaling = true;
    
    [Header("Debug")]
    public bool enableDebugLogs = false;
    
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Vector3 initialScale;
    private bool isGrabbed = false;
    
    void Awake()
    {
        // Setup XR Grab Interactable
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }
        
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        
        // Configure grab interactable
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        grabInteractable.throwOnDetach = false;
        
        initialScale = transform.localScale;
        
        // Register events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Initialized for {gameObject.name}");
    }
    
    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
    
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Grabbed by {args.interactorObject}");
    }
    
    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Released");
    }
    
    void Update()
    {
        if (!isGrabbed || grabInteractable.interactorsSelecting.Count == 0)
            return;
        
        // Get the first interactor (controller)
        var interactor = grabInteractable.interactorsSelecting[0];
        
        // Try to get controller from interactor
        var controller = interactor as XRBaseControllerInteractor;
        if (controller != null && controller.xrController != null)
        {
            // Thumbstick rotation around Y-axis
            if (enableThumbstickRotation)
            {
                Vector2 thumbstick = Vector2.zero;
                if (controller.xrController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out thumbstick))
                {
                    if (Mathf.Abs(thumbstick.x) > 0.1f)
                    {
                        float rotationAmount = thumbstick.x * rotationSpeed * Time.deltaTime;
                        transform.Rotate(Vector3.up, rotationAmount, Space.World);
                        
                        if (enableDebugLogs && Mathf.Abs(thumbstick.x) > 0.5f)
                            Debug.Log($"[ScansManipulation] Rotating: {rotationAmount}");
                    }
                }
            }
            
            // Trigger scaling
            if (enableTriggerScaling)
            {
                float triggerValue = 0f;
                if (controller.xrController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out triggerValue))
                {
                    if (triggerValue > 0.5f)
                    {
                        // Scale up when trigger pressed
                        float scaleChange = scaleSpeed * Time.deltaTime;
                        Vector3 newScale = transform.localScale + Vector3.one * scaleChange;
                        newScale = Vector3.one * Mathf.Clamp(newScale.x, minScale, maxScale);
                        transform.localScale = newScale;
                        
                        if (enableDebugLogs)
                            Debug.Log($"[ScansManipulation] Scaling up to {newScale.x}");
                    }
                }
                
                // Secondary button for scaling down
                bool secondaryButton = false;
                if (controller.xrController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out secondaryButton))
                {
                    if (secondaryButton)
                    {
                        float scaleChange = scaleSpeed * Time.deltaTime;
                        Vector3 newScale = transform.localScale - Vector3.one * scaleChange;
                        newScale = Vector3.one * Mathf.Clamp(newScale.x, minScale, maxScale);
                        transform.localScale = newScale;
                        
                        if (enableDebugLogs)
                            Debug.Log($"[ScansManipulation] Scaling down to {newScale.x}");
                    }
                }
            }
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
        if (enableDebugLogs) Debug.Log($"[ScansManipulation] Reset transform");
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

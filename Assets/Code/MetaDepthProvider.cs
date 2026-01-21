// MetaDepthProviderURP.cs
using UnityEngine;
using Meta.XR.EnvironmentDepth; // EnvironmentDepthManager
using changeDetection.Recording;

public enum DepthEye { Left = 0, Right = 1 }

public class MetaDepthProviderURP : MonoBehaviour, IDepthProvider
{
    [Header("References")]
    [SerializeField] private EnvironmentDepthManager envDepthManager;
    [SerializeField] private MonoBehaviour cameraFeedBehaviour;

    [Header("Options")]
    [SerializeField] private DepthEye eye = DepthEye.Left;
    [SerializeField] private Vector2Int fallbackSize = new Vector2Int(512, 512);
    [SerializeField] [Tooltip("Horizontal FOV in degrees for both RGB and Depth cameras (typical: 85-95°)")]
    private float horizontalFov = 90f;

    private ICameraFeed _feed;
    private bool _hasIntrinsics;
    private CameraIntrinsics _lastIntrinsics;

    private RenderTexture _singleEyeRT; // 2D RFloat copy target

    public bool IsReady => envDepthManager != null && envDepthManager.IsDepthAvailable;

    [SerializeField] private Material copyArraySliceMaterial;
    static readonly int SliceID = Shader.PropertyToID("_Slice");

    void Awake()
    {
        _feed = cameraFeedBehaviour as ICameraFeed;
        if (_feed != null) _feed.OnFrame += OnFeedFrame;
    }

    void OnDestroy()
    {
        if (_feed != null) _feed.OnFrame -= OnFeedFrame;
        if (_singleEyeRT) { _singleEyeRT.Release(); _singleEyeRT = null; }
    }

    void OnFeedFrame(CameraFrame f)
    {
        // Cache intrinsics from the camera feed’s frames
        _lastIntrinsics = f.intrinsics;
        _hasIntrinsics = true;
    }

    public bool TryGetDepthTexture(out RenderTexture depthRT)
    {
        depthRT = null;
        if (envDepthManager == null || !envDepthManager.IsDepthAvailable) return false;

        // The manager publishes the current depth as a global array texture:
        var src = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        if (src == null) return false;

        int w = src.width;
        int h = src.height;
        EnsureSingleEyeRT(w, h); // make a 2D RFloat RT (same size as src)

        // Always BLIT with a material that reads the array slice -> 2D
        // This avoids format mismatches (RHalf -> RFloat conversion happens in the shader).
        if (src.dimension == UnityEngine.Rendering.TextureDimension.Tex2DArray)
        {
            if (copyArraySliceMaterial == null)
            {
                Debug.LogWarning("MetaDepthProviderURP: copyArraySliceMaterial not assigned.");
                return false;
            }
            copyArraySliceMaterial.SetFloat(SliceID, (int)eye);
            // The shader samples the GLOBAL _EnvironmentDepthTexture;
            Graphics.Blit(null as Texture, _singleEyeRT, copyArraySliceMaterial);
        }
        else
        {
            // Fallback: if the global were 2D (rare), plain Blit is fine.
            Graphics.Blit(src, _singleEyeRT);
        }

        depthRT = _singleEyeRT;
        return true;
    }

    public DepthMeta GetDepthMeta()
    {
        int w = _singleEyeRT ? _singleEyeRT.width  : fallbackSize.x;
        int h = _singleEyeRT ? _singleEyeRT.height : fallbackSize.y;

        // Calculate depth camera intrinsics from FOV
        // fx = width / (2 * tan(hfov/2))
        float hfovRad = horizontalFov * Mathf.Deg2Rad;
        float fx = w / (2f * Mathf.Tan(hfovRad * 0.5f));
        
        // Estimate vertical FOV from aspect ratio
        float aspectRatio = (float)w / h;
        float vfovRad = 2f * Mathf.Atan(Mathf.Tan(hfovRad * 0.5f) / aspectRatio);
        float fy = h / (2f * Mathf.Tan(vfovRad * 0.5f));

        var intr = new CameraIntrinsics 
        { 
            width = w, 
            height = h, 
            fx = fx, 
            fy = fy, 
            cx = w * 0.5f, 
            cy = h * 0.5f, 
            distortion = Vector4.zero 
        };

        return new DepthMeta
        {
            valid         = (w > 0 && h > 0),
            width         = w,
            height        = h,
            format        = "R32F_meters",
            metersPerUnit = 1.0f,
            intrinsics    = intr
        };
    }

    void EnsureSingleEyeRT(int w, int h)
    {
        if (_singleEyeRT != null && (_singleEyeRT.width == w && _singleEyeRT.height == h)) return;
        if (_singleEyeRT != null) { _singleEyeRT.Release(); _singleEyeRT = null; }
        _singleEyeRT = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat)
        {
            name = "EnvDepth_SingleEye_RFloat",
            useMipMap = false,
            autoGenerateMips = false
        };
        _singleEyeRT.Create();
    }
}

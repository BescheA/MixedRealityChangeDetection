// Replace ONLY the MetaCameraFeedBB class in MetaCameraFeed.cs with this version.
using System;
using UnityEngine;
using UnityEngine.XR;
using PassthroughCameraSamples;

public class MetaCameraFeedBB : MonoBehaviour, ICameraFeed
{
    [Header("References")]
    [Tooltip("Assign the WebCamTextureManager in your scene (from the PCA sample).")]
    public WebCamTextureManager webCamManager;

    [Header("Options")]
    public bool autoStart = true;
    public PassthroughCameraEye eye = PassthroughCameraEye.Left;   // Left/Right are supported by the manager
    [Tooltip("Requested camera resolution (0,0 uses the largest available).")]
    public Vector2Int requestedResolution = new Vector2Int(1280, 960);

    public bool IsReady { get; private set; }
    public event Action<CameraFrame> OnFrame;

    Texture2D _cpuReadableTex; // RGBA32 readable buffer
    int _w, _h;

    // Cached intrinsics (scaled to current WebCamTexture size)
    CameraIntrinsics _intrinsics;
    bool _gotIntrinsicsAtThisSize;

    void Reset()
    {
        if (!webCamManager) webCamManager = FindFirstObjectByType<WebCamTextureManager>();
    }

    void Awake()
    {
        if (!webCamManager)
            webCamManager = FindFirstObjectByType<WebCamTextureManager>();
    }

    void Start()
    {
        if (autoStart) StartFeed();
    }

    public void StartFeed()
    {
        if (!webCamManager)
        {
            Debug.LogError("MetaCameraFeedBB: Missing WebCamTextureManager reference. Drop the PCA prefab into the scene and assign it.");
            return;
        }
        Debug.Log("[Unity] MetaCameraFeedBB: Starting camera feed.");

        // Configure which eye & target resolution BEFORE enabling so OnEnable uses them.
        webCamManager.Eye = eye;
        webCamManager.RequestedResolution = requestedResolution;

        // Ensure the manager is enabled; its OnEnable will request perms and start WebCamTexture.
        if (!webCamManager.enabled) webCamManager.enabled = true; // uses its OnEnable()/InitializeWebCamTexture(), which creates & plays the WebCamTexture
        IsReady = true;
    }

    public void StopFeed()
    {
        IsReady = false;

        // Disabling the manager triggers its OnDisable: Stop() & Destroy() the WebCamTexture.
        if (webCamManager) webCamManager.enabled = false;

        if (_cpuReadableTex != null)
        {
            Destroy(_cpuReadableTex);
            _cpuReadableTex = null;
        }

        _gotIntrinsicsAtThisSize = false;
    }

    void Update()
    {
        if (!IsReady || webCamManager == null) return;

        var wct = webCamManager.WebCamTexture;
        if (wct == null || !wct.didUpdateThisFrame) return;

        // Prepare a readable RGBA32 buffer that matches current size
        if (_cpuReadableTex == null || _w != wct.width || _h != wct.height)
        {
            _w = Mathf.Max(1, wct.width);
            _h = Mathf.Max(1, wct.height);
            if (_cpuReadableTex != null) Destroy(_cpuReadableTex);
            _cpuReadableTex = new Texture2D(_w, _h, TextureFormat.RGBA32, false, false);
            _gotIntrinsicsAtThisSize = false; // recalc intrinsics if size changes
        }

        // Copy current WebCamTexture frame -> CPU texture
        var tempRT = RenderTexture.GetTemporary(_w, _h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(wct, tempRT);
        var prev = RenderTexture.active;
        RenderTexture.active = tempRT;
        _cpuReadableTex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0, false);
        _cpuReadableTex.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tempRT);

        // Intrinsics: PassthroughCameraUtils returns values for the MAX sensor resolution.
        // We scale them to the CURRENT WebCamTexture resolution.
        if (!_gotIntrinsicsAtThisSize)
        {
            // Only available on Quest 3 / 3S with supported Horizon OS (the util handles checks).
            var p = PassthroughCameraUtils.GetCameraIntrinsics(eye);                          // exists
            // p.Resolution is "max sensor resolution" for which intrinsics are defined.
            float sx = _w / Mathf.Max(1f, p.Resolution.x);
            float sy = _h / Mathf.Max(1f, p.Resolution.y);

            _intrinsics = new CameraIntrinsics
            {
                width = _w,
                height = _h,
                fx = p.FocalLength.x * sx,
                fy = p.FocalLength.y * sy,
                cx = p.PrincipalPoint.x * sx,
                cy = p.PrincipalPoint.y * sy,
                // Distortion not exposed by Utils; store skew in .w as a convenience, others 0.
                distortion = new Vector4(0f, 0f, 0f, p.Skew)
            };
            _gotIntrinsicsAtThisSize = true;
        }

        // Head pose (world)
        var hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        hmd.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
        hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);

        // Timestamp: WebCamTexture doesn’t expose sensor timestamps; use realtime fallback.
        long tsNs = (long)(Time.realtimeSinceStartupAsDouble * 1e9);

        var frame = new CameraFrame
        {
            texture = _cpuReadableTex,
            intrinsics = _intrinsics,
            pose = new CameraPose { position_world = pos, rotation_world = rot },
            timestampNs = tsNs
        };
        OnFrame?.Invoke(frame);
    }
}

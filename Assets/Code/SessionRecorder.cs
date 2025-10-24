using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro; // AsyncGPUReadback for depth
using changeDetection.Recording;

public class SessionRecorder : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour cameraFeedBehaviour;    // implements ICameraFeed (your existing feed)
    public MonoBehaviour depthProviderBehaviour; // implements IDepthProvider (see bottom)
    public Button recordButton;                  // optional toggle button
    public TextMeshPro recordButtonText;                // optional

    [Header("Optional on-screen preview (RawImage)")]
    public RawImage preview;                     // on-screen preview of color feed (optional)

    ICameraFeed _feed;
    IDepthProvider _depth;                       // optional: only used if assigned
    CancellationTokenSource _cts;
    volatile bool _recording;
    string _dir;
    int _idx;

    // MAIN-THREAD encode queues
    readonly ConcurrentQueue<(Texture2D tex, CameraFrame meta, int idx)> _encodeColorQ = new();
    readonly ConcurrentQueue<(Texture2D tex, DepthMeta meta, int idx)>   _encodeDepthQ = new();

    // WORKER write queues
    readonly ConcurrentQueue<(byte[] png,  CameraFrame meta, int idx)> _writeColorQ = new();
    readonly ConcurrentQueue<(byte[] exr,  DepthMeta meta,   int idx)> _writeDepthQ = new();

    void Awake()
    {
        _feed  = cameraFeedBehaviour  as ICameraFeed;
        _depth = depthProviderBehaviour as IDepthProvider;

        if (_feed != null) _feed.OnFrame += OnFrame;
        if (recordButton) recordButton.onClick.AddListener(Toggle);
    }

    void OnDestroy()
    {
        if (_feed != null) _feed.OnFrame -= OnFrame;
        _cts?.Cancel();
    }

    public void Toggle() => (_recording ? (Action)StopRecording : StartRecording)();

    public void StartRecording()
    {
        if (_feed == null || !_feed.IsReady)
        {
            Debug.LogWarning("Recorder: camera feed not ready.");
            return;
        }

        if (_depth != null && !_depth.IsReady)
        {
            Debug.LogWarning("Recorder: depth provider present but not ready; depth will be skipped.");
        }

        _idx = 0;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _dir = Path.Combine(Application.persistentDataPath, $"session_{stamp}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "session_info.json"), "{\"version\":2}");

        _cts = new CancellationTokenSource();
        _recording = true;
        if (recordButtonText) recordButtonText.text = "Stop Recording";
        _ = Task.Run(() => WriterLoop(_cts.Token));

        Debug.Log("Recording → " + _dir);
    }

    public void StopRecording()
    {
        _recording = false;
        if (recordButtonText) recordButtonText.text = "Start Recording";
        _cts?.Cancel();
        _cts = null;
        Debug.Log("Recording stopped.");
    }

    void OnFrame(CameraFrame f)
    {
        // Feed invokes this from its Update() — already on main thread
        if (preview) preview.texture = f.texture;
        if (!_recording) return;

        int idx = Interlocked.Increment(ref _idx);

        // --- COLOR: enqueue CPU-readable copy for PNG encoding on the main thread
        var raw = f.texture.GetRawTextureData<byte>();
        byte[] copy = raw.ToArray(); // allocate once per recorded frame
        _encodeColorQ.Enqueue((MakeTempRGBA(f.texture.width, f.texture.height, copy), f, idx));

        // --- DEPTH: only if provider available & ready
        if (_depth != null && _depth.IsReady && _depth.TryGetDepthTexture(out var depthRT))
        {
            // Do GPU->CPU asynchronously to avoid hitches
            AsyncGPUReadback.Request(depthRT, 0, (AsyncGPUReadbackRequest req) =>
            {
                if (req.hasError) { Debug.LogWarning("Depth readback error"); return; }
                try
                {
                    var dm = _depth.GetDepthMeta(); // includes depth intrinsics + units/format
                    Texture2D depthCPU = new Texture2D(depthRT.width, depthRT.height, TextureFormat.RFloat, false, true);
                    depthCPU.LoadRawTextureData(req.GetData<float>());
                    depthCPU.Apply(false, false);
                    _encodeDepthQ.Enqueue((depthCPU, dm, idx));
                }
                catch (Exception e)
                {
                    Debug.LogError("Depth enqueue failed: " + e);
                }
            });
        }

        // local helper
        Texture2D MakeTempRGBA(int w, int h, byte[] rgba)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            t.LoadRawTextureData(rgba);
            t.Apply(false, false);
            return t;
        }
    }

    void Update()
    {
        // throttle encodes to avoid main-thread spikes
        const int maxColorEncodesPerFrame = 2;
        const int maxDepthEncodesPerFrame = 2;

        for (int n = 0; n < maxColorEncodesPerFrame; n++)
        {
            if (!_encodeColorQ.TryDequeue(out var item)) break;
            byte[] pngBytes = item.tex.EncodeToPNG();
            _writeColorQ.Enqueue((pngBytes, item.meta, item.idx));
        }

        for (int n = 0; n < maxDepthEncodesPerFrame; n++)
        {
            if (!_encodeDepthQ.TryDequeue(out var d)) break;
            // EXR preserves float depth (lossless)
            byte[] exrBytes = ImageConversion.EncodeToEXR(d.tex, Texture2D.EXRFlags.OutputAsFloat);
            _writeDepthQ.Enqueue((exrBytes, d.meta, d.idx));
        }
    }

    async Task WriterLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested || !_writeColorQ.IsEmpty || !_writeDepthQ.IsEmpty)
        {
            bool didWork = false;

            if (_writeColorQ.TryDequeue(out var c))
            {
                didWork = true;
                try
                {
                    string baseName = $"frame_{c.idx:000000}";
                    string pngPath  = Path.Combine(_dir, baseName + ".png");
                    string jsonPath = Path.Combine(_dir, baseName + ".json");

                    await File.WriteAllBytesAsync(pngPath, c.png, ct);

                    // Write/merge JSON for color + pose
                    string json = BuildOrMergeJson(jsonPath, c.meta, pngPath, null);
                    await File.WriteAllTextAsync(jsonPath, json, ct);
                }
                catch (Exception e)
                {
                    Debug.LogError("WriterLoop(Color): " + e);
                }
            }

            if (_writeDepthQ.TryDequeue(out var d))
            {
                didWork = true;
                try
                {
                    string baseName  = $"frame_{d.idx:000000}";
                    string depthPath = Path.Combine(_dir, baseName + "_depth.exr");
                    string jsonPath  = Path.Combine(_dir, baseName + ".json");

                    await File.WriteAllBytesAsync(depthPath, d.exr, ct);

                    // Merge in (or add) the depth block
                    string json = BuildOrMergeJson(jsonPath, null, d.meta, null, depthPath);
                    await File.WriteAllTextAsync(jsonPath, json, ct);
                }
                catch (Exception e)
                {
                    Debug.LogError("WriterLoop(Depth): " + e);
                }
            }

            if (!didWork) await Task.Delay(1, ct);
        }
    }
    string BuildOrMergeJson(string jsonPath, CameraFrame? colorMeta, DepthMeta depthMeta, string colorPath, string depthPath)
    {
        FrameJson f;

        if (File.Exists(jsonPath))
        {
            try { f = JsonUtility.FromJson<FrameJson>(File.ReadAllText(jsonPath)); }
            catch { f = new FrameJson(); }
        }
        else f = new FrameJson();

        if (colorMeta.HasValue)
        {
            var m = colorMeta.Value;
            f.timestamp_ns = m.timestampNs;

            f.intrinsics = new IntrinsicsDTO
            {
                width  = m.intrinsics.width,
                height = m.intrinsics.height,
                fx = m.intrinsics.fx, fy = m.intrinsics.fy,
                cx = m.intrinsics.cx, cy = m.intrinsics.cy,
                distortion = new float[] {
                    m.intrinsics.distortion.x, m.intrinsics.distortion.y,
                    m.intrinsics.distortion.z, m.intrinsics.distortion.w
                }
            };

            f.pose = new PoseDTO
            {
                position = new float[] { m.pose.position_world.x, m.pose.position_world.y, m.pose.position_world.z },
                rotation = new float[] { m.pose.rotation_world.x, m.pose.rotation_world.y, m.pose.rotation_world.z, m.pose.rotation_world.w }
            };
        }
/*
        if (!string.IsNullOrEmpty(colorPath))
        {
            
        }
*/
        if (!string.IsNullOrEmpty(depthPath) && depthMeta.valid)
        {
            f.depth = new DepthDTO
            {
                path = Path.GetFileName(depthPath),
                width = depthMeta.width,
                height = depthMeta.height,
                format = depthMeta.format,
                meters_per_unit = depthMeta.metersPerUnit,
                intrinsics = new IntrinsicsDTO
                {
                    width  = depthMeta.intrinsics.width,
                    height = depthMeta.intrinsics.height,
                    fx = depthMeta.intrinsics.fx, fy = depthMeta.intrinsics.fy,
                    cx = depthMeta.intrinsics.cx, cy = depthMeta.intrinsics.cy,
                    distortion = new float[] {
                        depthMeta.intrinsics.distortion.x, depthMeta.intrinsics.distortion.y,
                        depthMeta.intrinsics.distortion.z, depthMeta.intrinsics.distortion.w
                    }
                }
            };
        }

        return JsonUtility.ToJson(f, false);
    }
    string BuildOrMergeJson(string jsonPath, CameraFrame? colorMeta, string colorPath, string depthPath)
    {
        FrameJson f;

        if (File.Exists(jsonPath))
        {
            try { f = JsonUtility.FromJson<FrameJson>(File.ReadAllText(jsonPath)); }
            catch { f = new FrameJson(); }
        }
        else f = new FrameJson();

        if (colorMeta.HasValue)
        {
            var m = colorMeta.Value;
            f.timestamp_ns = m.timestampNs;

            f.intrinsics = new IntrinsicsDTO
            {
                width  = m.intrinsics.width,
                height = m.intrinsics.height,
                fx = m.intrinsics.fx, fy = m.intrinsics.fy,
                cx = m.intrinsics.cx, cy = m.intrinsics.cy,
                distortion = new float[] {
                    m.intrinsics.distortion.x, m.intrinsics.distortion.y,
                    m.intrinsics.distortion.z, m.intrinsics.distortion.w
                }
            };

            f.pose = new PoseDTO
            {
                position = new float[] { m.pose.position_world.x, m.pose.position_world.y, m.pose.position_world.z },
                rotation = new float[] { m.pose.rotation_world.x, m.pose.rotation_world.y, m.pose.rotation_world.z, m.pose.rotation_world.w }
            };
        }
/*
        if (!string.IsNullOrEmpty(colorPath))
        {

        }
*/
        return JsonUtility.ToJson(f, false);
    }

    [Serializable] struct FrameJson
    {
        public long timestamp_ns;
        public IntrinsicsDTO intrinsics;
        public PoseDTO       pose;
        public DepthDTO      depth;
        // public string color_path; // uncomment if you choose to store it
    }
    [Serializable] struct IntrinsicsDTO
    {
        public int width, height;
        public float fx, fy, cx, cy;
        public float[] distortion; // [k1,k2,k3,skew] per your feed’s writeout
    }
    [Serializable] struct PoseDTO
    {
        public float[] position; // world meters
        public float[] rotation; // quaternion (x,y,z,w)
    }
    [Serializable] struct DepthDTO
    {
        public string path; // "<frame>_depth.exr"
        public int width, height;
        public string format; // e.g., "R32F_meters" / "R16F_millimeters"
        public float meters_per_unit; // 1.0 if already meters
        public IntrinsicsDTO intrinsics;
    }
}


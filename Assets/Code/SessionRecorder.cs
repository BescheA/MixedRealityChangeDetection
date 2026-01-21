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
using UnityEngine.InputSystem;

public class SessionRecorder : MonoBehaviour
{
    [Header("References")]
    public InputActionReference recordAction; // optional input action to toggle recording
    public MonoBehaviour cameraFeedBehaviour;    // implements ICameraFeed (your existing feed)
    public MonoBehaviour depthProviderBehaviour; // implements IDepthProvider (see bottom)
    public Button recordButton;                  // optional toggle button
    public TextMeshPro recordButtonText;                // optional
    public TextMeshProUGUI infoText;                 // info display: "Session Started", "Frames collected", etc.

    [Header("Optional on-screen preview (RawImage)")]
    public RawImage preview;                     // on-screen preview of color feed (optional)

    ICameraFeed _feed;
    IDepthProvider _depth;                       // optional: only used if assigned
    CancellationTokenSource _cts;
    volatile bool _recording;
    string _dir;
    int _idx;
    DateTime _recordingStartTime;

    // MAIN-THREAD encode queues
    readonly ConcurrentQueue<(Texture2D tex, CameraFrame meta, int idx)> _encodeColorQ = new();
    readonly ConcurrentQueue<(Texture2D tex, DepthMeta meta, int idx)>   _encodeDepthQ = new();

    // WORKER write queues
    readonly ConcurrentQueue<(byte[] png,  CameraFrame meta, int idx)> _writeColorQ = new();
    readonly ConcurrentQueue<(byte[] exr,  DepthMeta meta,   int idx)> _writeDepthQ = new();

    // Writer thread handle
    private Thread _writerThread;
    private volatile bool _writerThreadRunning;

    void Awake()
    {
        _feed  = cameraFeedBehaviour  as ICameraFeed;
        _depth = depthProviderBehaviour as IDepthProvider;

        if (_feed != null) _feed.OnFrame += OnFrame;
    }
    private void OnEnable() {
        /*if(recordAction != null) {
            recordAction.action.performed += OnRecordActionPerformed;
            recordAction.action.Enable();
        }*/
    }

    private void OnRecordActionPerformed(InputAction.CallbackContext context)
    {
        //Toggle();
    }

    void OnDestroy()
    {
        if (_feed != null) _feed.OnFrame -= OnFrame;
        _cts?.Cancel();
        StopEncoderThread();
        _writerThreadRunning = false;
        if (_writerThread != null)
        {
            _writerThread.Join(TimeSpan.FromSeconds(5));
        }
    }


    // Toggle is not used anymore. Use StartRecording and StopRecording directly from UI.

    public void StartRecording()
    {
        // Idempotent: do nothing if already recording
        if (_recording) return;

        // Start camera feed if not running
        if (_feed == null || !_feed.IsReady)
        {
            Debug.Log("SessionRecorder: Camera feed not ready, starting feed...");
            MonoBehaviour camMono = cameraFeedBehaviour as MonoBehaviour;
            if (camMono != null)
            {
                var startMethod = camMono.GetType().GetMethod("StartFeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (startMethod != null)
                {
                    startMethod.Invoke(camMono, null);
                }
            }
            // Wait for feed to become ready (simple poll, could be improved with event/callback)
            float waitStart = Time.realtimeSinceStartup;
            float timeout = 5f;
            while (_feed != null && !_feed.IsReady && (Time.realtimeSinceStartup - waitStart) < timeout)
            {
                System.Threading.Thread.Sleep(50);
            }
            if (_feed == null || !_feed.IsReady)
            {
                Debug.LogError("SessionRecorder: Camera feed could not be started or is not ready.");
                return;
            }
        }

        if (_depth != null && !_depth.IsReady)
        {
            Debug.LogWarning("Recorder: depth provider present but not ready; depth will be skipped.");
        }

        _idx = 0;
        var stamp = DateTime.UtcNow.AddHours(1.0).ToString("yyyyMMdd_HHmmss"); // UTC+1
        _dir = Path.Combine(Application.persistentDataPath, $"session_{stamp}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "session_info.json"), "{\"version\":2}");

        _cts = new CancellationTokenSource();
        _recording = true;
        _recordingStartTime = DateTime.UtcNow.AddHours(1.0); // UTC+1

        if (recordButtonText) recordButtonText.text = "Stop Recording";

        // Start encoder thread (moves encoding from main thread to background)
        StartEncoderThread();

        // Start writer thread (handles file I/O asynchronously)
        _writerThreadRunning = true;
        _writerThread = new Thread(() => WriterLoopThread(_cts.Token))
        {
            Name = "SessionRecorder.WriterThread",
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal // Low priority to not interfere with main thread
        };
        _writerThread.Start();

        // Update info text
        UpdateInfoText($"Session Started\nTimestamp: {_recordingStartTime:yyyy-MM-dd HH:mm:ss}");

        Debug.Log("Recording → " + _dir);
    }

    public void StopRecording()
    {
        // Idempotent: do nothing if not recording
        if (!_recording) return;

        _recording = false;
        if (recordButtonText) recordButtonText.text = "Start Recording";

        // Calculate duration and total frames collected
        DateTime endTime = DateTime.UtcNow.AddHours(1.0); // UTC+1
        TimeSpan duration = endTime - _recordingStartTime;
        int totalFrames = _idx;

        // Stop encoder thread (allow it to finish encoding queued items)
        StopEncoderThread();

        // Stop writer thread (allow it to finish writing queued items)
        _writerThreadRunning = false;
        if (_writerThread != null)
        {
            _writerThread.Join(TimeSpan.FromSeconds(5));
            _writerThread = null;
        }

        _cts?.Cancel();
        _cts = null;

        // Update info text with session summary
        UpdateInfoText($"Session Ended\nFrames Collected: {totalFrames}\nDuration: {duration:hh\\:mm\\:ss}\nTimestamp: {endTime:yyyy-MM-dd HH:mm:ss}");
        Debug.Log("Recording stopped.");

        // Stop camera feed if running
        if (_feed != null && _feed.IsReady)
        {
            MonoBehaviour camMono = cameraFeedBehaviour as MonoBehaviour;
            if (camMono != null)
            {
                var stopMethod = camMono.GetType().GetMethod("StopFeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (stopMethod != null)
                {
                    stopMethod.Invoke(camMono, null);
                }
            }
        }
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

    /// <summary>
    /// Helper method to update the info text display on the UI
    /// </summary>
    void UpdateInfoText(string message)
    {
        if (infoText != null)
        {
            infoText.text = message;
        }
    }


    // Encoder-Thread entfernt, Encoding läuft jetzt im Main Thread (Update)
    // Dummy-Methoden für Kompatibilität (werden nicht mehr genutzt)
    void StartEncoderThread() { }
    void StopEncoderThread() { }

    void Update()
    {
        if (!_recording) return;

        // Encoding jetzt im Main Thread: PNG/EXR Encoding und Übergabe an Write-Queue
        int maxFramesPerUpdate = 4; // Limitiert, um Framedrops zu vermeiden
        int framesProcessed = 0;

        while (!_encodeColorQ.IsEmpty && framesProcessed < maxFramesPerUpdate)
        {
            if (_encodeColorQ.TryDequeue(out var colorItem))
            {
                try
                {
                    byte[] pngBytes = colorItem.tex.EncodeToPNG();
                    _writeColorQ.Enqueue((pngBytes, colorItem.meta, colorItem.idx));
                }
                catch (Exception e)
                {
                    Debug.LogError("MainThread EncodeToPNG: " + e);
                }
                finally
                {
                    try { UnityEngine.Object.Destroy(colorItem.tex); } catch { }
                }
                framesProcessed++;
            }
        }

        framesProcessed = 0;
        while (!_encodeDepthQ.IsEmpty && framesProcessed < maxFramesPerUpdate)
        {
            if (_encodeDepthQ.TryDequeue(out var depthItem))
            {
                try
                {
                    byte[] exrBytes = ImageConversion.EncodeToEXR(depthItem.tex, Texture2D.EXRFlags.OutputAsFloat);
                    _writeDepthQ.Enqueue((exrBytes, depthItem.meta, depthItem.idx));
                }
                catch (Exception e)
                {
                    Debug.LogError("MainThread EncodeToEXR: " + e);
                }
                finally
                {
                    try { UnityEngine.Object.Destroy(depthItem.tex); } catch { }
                }
                framesProcessed++;
            }
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
                    string baseNameColor = $"{c.idx:000000}_color";
                    string baseNameMetaData = $"{c.idx:000000}_pose";
                    string pngPath  = Path.Combine(_dir, baseNameColor + ".png");
                    string jsonPath = Path.Combine(_dir, baseNameMetaData + ".json");

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
                    string baseName = $"{d.idx:000000}_depth";
                    string baseNameMetaData = $"{d.idx:000000}_pose";
                    string depthPath = Path.Combine(_dir, baseName + "_depth.exr");
                    string jsonPath  = Path.Combine(_dir, baseNameMetaData + ".json");

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

    /// <summary>
    /// Writer thread loop using synchronous I/O (better for threads than async/await)
    /// </summary>
    void WriterLoopThread(CancellationToken ct)
    {
        try
        {
            while (_writerThreadRunning && (!ct.IsCancellationRequested || !_writeColorQ.IsEmpty || !_writeDepthQ.IsEmpty))
            {
                bool didWork = false;

                // Process color frames
                if (_writeColorQ.TryDequeue(out var c))
                {
                    didWork = true;
                    try
                    {
                        string baseNameColor = $"{c.idx:000000}_color";
                        string baseNameMetaData = $"{c.idx:000000}_pose";
                        string pngPath = Path.Combine(_dir, baseNameColor + ".png");
                        string jsonPath = Path.Combine(_dir, baseNameMetaData + ".json");

                        // Synchronous I/O on background thread (avoids async overhead)
                        File.WriteAllBytes(pngPath, c.png);

                        // Write/merge JSON for color + pose
                        string json = BuildOrMergeJson(jsonPath, c.meta, pngPath, null);
                        File.WriteAllText(jsonPath, json, Encoding.UTF8);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("WriterLoopThread(Color): " + e);
                    }
                }

                // Process depth frames
                if (_writeDepthQ.TryDequeue(out var d))
                {
                    didWork = true;
                    try
                    {
                        string baseName = $"{d.idx:000000}_depth";
                        string baseNameMetaData = $"{d.idx:000000}_pose";
                        string depthPath = Path.Combine(_dir, baseName + "_depth.exr");
                        string jsonPath = Path.Combine(_dir, baseNameMetaData + ".json");

                        // Synchronous I/O on background thread
                        File.WriteAllBytes(depthPath, d.exr);

                        // Merge in (or add) the depth block
                        string json = BuildOrMergeJson(jsonPath, null, d.meta, null, depthPath);
                        File.WriteAllText(jsonPath, json, Encoding.UTF8);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("WriterLoopThread(Depth): " + e);
                    }
                }

                // Yield to other threads if no work
                if (!didWork)
                {
                    Thread.Sleep(1);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("WriterLoopThread crashed: " + e);
        }
        finally
        {
            _writerThreadRunning = false;
            Debug.Log("WriterLoopThread finished");
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
            Matrix4x4 rotationMatrix = Matrix4x4.Rotate(m.pose.rotation_world);
            rotationMatrix.m03 = m.pose.position_world.x;
            rotationMatrix.m13 = m.pose.position_world.y;
            rotationMatrix.m23 = m.pose.position_world.z;
            f.pose = new PoseDTO
            {
                //position = new float[] { m.pose.position_world.x, m.pose.position_world.y, m.pose.position_world.z },
                extrinsics = rotationMatrix
                //rotation = new float[] { m.pose.rotation_world.x, m.pose.rotation_world.y, m.pose.rotation_world.z, m.pose.rotation_world.w }
            };

            //Matrix4x4 rotationMatrix = Matrix4x4.Rotate(m.pose.rotation_world);
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
            Matrix4x4 rotationMatrix = Matrix4x4.Rotate(m.pose.rotation_world);
            rotationMatrix.m03 = m.pose.position_world.x;
            rotationMatrix.m13 = m.pose.position_world.y;
            rotationMatrix.m23 = m.pose.position_world.z;
            f.pose = new PoseDTO
            {
                //position = new float[] { m.pose.position_world.x, m.pose.position_world.y, m.pose.position_world.z },
                extrinsics = rotationMatrix
                //rotation = new float[] { m.pose.rotation_world.x, m.pose.rotation_world.y, m.pose.rotation_world.z, m.pose.rotation_world.w }
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
        //public float[] position; // world meters
        public Matrix4x4 extrinsics; // quaternion (x,y,z,w)
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


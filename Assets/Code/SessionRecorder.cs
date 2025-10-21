using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class SessionRecorder : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour cameraFeedBehaviour;   // assign QuestWebCamFeed (implements ICameraFeed)
    public Button recordButton;                 // optional toggle button
    public Text recordButtonText;               // optional

    [Header("Optional on-screen preview (RawImage)")]
    public RawImage preview;                    // assign to see the feed; safe to leave null

    ICameraFeed _feed;
    CancellationTokenSource _cts;
    volatile bool _recording;
    string _dir;
    int _idx;

    // MAIN THREAD queue: we encode PNG here (safe).
    readonly ConcurrentQueue<(Texture2D tex, CameraFrame meta, int idx)> _encodeQ = new();
    // WORKER queue: just bytes for disk write (no Unity API here).
    readonly ConcurrentQueue<(byte[] png, CameraFrame meta, int idx)> _writeQ = new();

    void Awake()
    {
        _feed = cameraFeedBehaviour as ICameraFeed;
        if (_feed != null) _feed.OnFrame += OnFrame;
        if (recordButton) recordButton.onClick.AddListener(Toggle);
    }

    void OnDestroy()
    {
        if (_feed != null) _feed.OnFrame -= OnFrame;
        _cts?.Cancel();
    }

    // API Endpoint to toggle recording via button / toggle
    public void Toggle() => (_recording ? (Action)StopRecording : StartRecording)();


    // Use Toggle to automatically switch between Start/Stop
    public void StartRecording()
    {
        if (_feed == null || !_feed.IsReady)
        {
            Debug.LogWarning("Recorder: camera feed not ready.");
            return;
        }

        _idx = 0;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _dir = Path.Combine(Application.persistentDataPath, $"session_{stamp}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "session_info.json"), "{\"version\":1}");

        _cts = new CancellationTokenSource();
        _recording = true;
        if (recordButtonText) recordButtonText.text = "Stop Recording";
        _ = Task.Run(() => WriterLoop(_cts.Token));

        Debug.Log("Recording → " + _dir);
    }
    
    // Use Toggle to automatically switch between Start/Stop
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
        // This callback should already be on the main thread (our feed invokes it from Update()).
        if (preview) preview.texture = f.texture;
        if (!_recording) return;

        int idx = Interlocked.Increment(ref _idx);

        // Enqueue the Texture2D reference for MAIN-THREAD encoding (done in Update()).
        var raw = f.texture.GetRawTextureData<byte>();
        byte[] copy = raw.ToArray(); // allocate once per frame recorded

        _encodeQ.Enqueue(( RecreateTempTexture(f.texture.width, f.texture.height, copy), f, idx ));

        // Helper: make a temporary Texture2D from raw bytes.
        Texture2D RecreateTempTexture(int w, int h, byte[] rgba)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            t.LoadRawTextureData(rgba);
            t.Apply(false, false);
            return t;
        }
    }

    void Update()
    {
        // Do at most N encodes per frame to avoid hitches.
        const int maxEncodesPerFrame = 2;

        for (int n = 0; n < maxEncodesPerFrame; n++)
        {
            if (!_encodeQ.TryDequeue(out var item)) break;

            // IMPORTANT: EncodeToPNG is a Unity API ergo MUST run on main thread.
            byte[] pngBytes = item.tex.EncodeToPNG();

            _writeQ.Enqueue((pngBytes, item.meta, item.idx));
        }
    }

    async Task WriterLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested || !_writeQ.IsEmpty)
        {
            if (_writeQ.TryDequeue(out var item))
            {
                try
                {
                    string baseName = $"frame_{item.idx:000000}";
                    string pngPath  = Path.Combine(_dir, baseName + ".png");
                    string jsonPath = Path.Combine(_dir, baseName + ".json");

                    await File.WriteAllBytesAsync(pngPath, item.png, ct);

                    var m = item.meta;
                    var sb = new StringBuilder(256);
                    sb.Append("{\"timestamp_ns\":").Append(m.timestampNs).Append(',');
                    sb.Append("\"intrinsics\":{");
                    sb.AppendFormat("\"width\":{0},\"height\":{1},\"fx\":{2},\"fy\":{3},\"cx\":{4},\"cy\":{5},\"distortion\":[{6},{7},{8},{9}]}}",
                        m.intrinsics.width, m.intrinsics.height, m.intrinsics.fx, m.intrinsics.fy,
                        m.intrinsics.cx, m.intrinsics.cy,
                        m.intrinsics.distortion.x, m.intrinsics.distortion.y, m.intrinsics.distortion.z, m.intrinsics.distortion.w);
                    sb.Append(",\"pose\":{");
                    sb.AppendFormat("\"position\":[{0},{1},{2}],", m.pose.position_world.x, m.pose.position_world.y, m.pose.position_world.z);
                    sb.AppendFormat("\"rotation\":[{0},{1},{2},{3}]}}",
                        m.pose.rotation_world.x, m.pose.rotation_world.y, m.pose.rotation_world.z, m.pose.rotation_world.w);

                    await File.WriteAllTextAsync(jsonPath, sb.ToString(), ct);
                }
                catch (Exception e)
                {
                    Debug.LogError("WriterLoop: " + e);
                }
            }
            else
            {
                await Task.Delay(1, ct);
            }
        }
    }
}

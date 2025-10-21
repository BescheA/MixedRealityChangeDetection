// Scripts/Capture/ICameraFeed.cs
using System;
using UnityEngine;

public interface ICameraFeed
{
    bool IsReady { get; }
    event Action<CameraFrame> OnFrame;

    // Start/stop the feed (permissions handled inside).
    void StartFeed();
    void StopFeed();
}

[Serializable]
public struct CameraIntrinsics
{
    public int width, height;
    public float fx, fy, cx, cy;
    public Vector4 distortion; // k1,k2,p1,p2 (or whatever the SDK provides)
}

[Serializable]
public struct CameraPose
{
    public Vector3 position_world;
    public Quaternion rotation_world;
}

[Serializable]
public struct CameraFrame
{
    public Texture2D texture;        // RGB frame for one eye (e.g., Left)
    public CameraIntrinsics intrinsics;
    public CameraPose pose;
    public long timestampNs;         // sensor timestamp
}

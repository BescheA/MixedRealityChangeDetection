using UnityEngine;
namespace changeDetection.Recording
{
    [System.Serializable]
    public struct DepthMeta
    {
        public bool   valid;
        public int    width, height;
        public string format;
        public float  metersPerUnit;
        public CameraIntrinsics intrinsics;
    }
}

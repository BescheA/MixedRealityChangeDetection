using UnityEngine;

namespace changeDetection.Recording
{
    public interface IDepthProvider
    {
        bool IsReady { get; }
        bool TryGetDepthTexture(out RenderTexture depthRT);
        DepthMeta GetDepthMeta();
    }
}

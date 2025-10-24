// YourApp/Recording/IRuntimeDepthProvider.cs
using UnityEngine;
namespace changeDetection.Recording
{
    public interface IRuntimeDepthProvider
    {
        bool IsReady { get; }
        bool TryGetDepthTexture(out RenderTexture depthRT);
        DepthMeta GetDepthMeta();
    }
}

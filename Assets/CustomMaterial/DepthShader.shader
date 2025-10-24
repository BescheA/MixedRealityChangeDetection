Shader "Hidden/DepthShader"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            // URP core includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Depth texture array published globally by EnvironmentDepthManager
            TEXTURE2D_ARRAY(_EnvironmentDepthTexture);
            SAMPLER(sampler_EnvironmentDepthTexture);

            // Which slice to read: 0 = Left, 1 = Right
            CBUFFER_START(UnityPerMaterial)
                float _Slice;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                // NOTE: URP macro requires (tex, sampler, uv, slice)
                float d = SAMPLE_TEXTURE2D_ARRAY(_EnvironmentDepthTexture,
                                                 sampler_EnvironmentDepthTexture,
                                                 i.uv, _Slice).r;

                // Write to R channel; your destination RT is RFloat
                return float4(d, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

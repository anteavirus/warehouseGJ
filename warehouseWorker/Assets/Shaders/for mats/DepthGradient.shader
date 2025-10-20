Shader "Custom/DepthGradient"
{
    Properties
    {
        _DarkColor("Dark Color", Color) = (0,0,0,1)
        _LightColor("Light Color", Color) = (1,1,1,1)
        _DepthStart("Depth Start", Float) = 0
        _DepthEnd("Depth End", Float) = 100
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _LightColor;
                float _DepthStart;
                float _DepthEnd;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Sample depth texture (URP method)
                float2 screenUV = IN.positionHCS.xy / _ScreenParams.xy;
                float depth = SampleSceneDepth(screenUV);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

                // Calculate gradient
                float gradient = saturate((linearDepth - _DepthStart) / (_DepthEnd - _DepthStart));
                
                // Blend colors
                return lerp(_DarkColor, _LightColor, gradient);
            }
            ENDHLSL
        }
    }
}

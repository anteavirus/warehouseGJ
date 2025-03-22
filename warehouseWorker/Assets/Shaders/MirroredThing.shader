Shader "Custom/MirroredThing"
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
        Tags { 
            "RenderType"="Transparent"  // Changed to Transparent
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"       // Render after opaque objects
        }

        Pass
        {
            Cull Off                    // Render both front/back faces
            ZWrite Off                  // Disable depth writing
            ZTest Always                // Always draw pixels (ignore depth)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
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
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Calculate screen UV with Y flipped
                float2 screenUV = IN.positionHCS.xy / _ScreenParams.xy;
                screenUV.y = 1.0 - screenUV.y;
                
                // Sample depth texture
                float depth = SampleSceneDepth(screenUV);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

                // Compute gradient and blend colors
                float gradient = saturate((linearDepth - _DepthStart) / (_DepthEnd - _DepthStart));
                return lerp(_DarkColor, _LightColor, gradient);
            }
            ENDHLSL
        }
    }
}

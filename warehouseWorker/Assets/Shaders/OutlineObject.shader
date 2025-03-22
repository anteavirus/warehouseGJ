Shader "Custom/URP_FullOutline_PS1"
{
    Properties
    {
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (1,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 0.2)) = 0.05
        _PixelSize("Pixel Size", Float) = 16
        _VertexSnapping("Vertex Snap", Float) = 8
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry+1" 
        }

        // HLSLINCLUDE to share declarations between passes
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Texture and Sampler
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Shared variables
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _PixelSize;
                float _VertexSnapping;
            CBUFFER_END
        ENDHLSL

        // Outline pass
        Pass
        {
            Name "Outline"
            Cull Front
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM
                #pragma vertex vert_outline
                #pragma fragment frag_outline

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float3 normalOS : NORMAL;
                };

                struct Varyings
                {
                    float4 positionHCS : SV_POSITION;
                };

                Varyings vert_outline(Attributes IN)
                {
                    Varyings OUT;
                    float3 extrudedPos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                    OUT.positionHCS = TransformObjectToHClip(extrudedPos);
                    OUT.positionHCS.xy = floor(OUT.positionHCS.xy * _PixelSize) / _PixelSize;
                    return OUT;
                }

                half4 frag_outline(Varyings IN) : SV_Target
                {
                    return _OutlineColor;
                }
            ENDHLSL
        }

        // Main pass
        Pass
        {
            Name "MainPass"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
                #pragma vertex vert_main
                #pragma fragment frag_main

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct Varyings
                {
                    float4 positionHCS : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };

                Varyings vert_main(Attributes IN)
                {
                    Varyings OUT;
                    float4 posOS = IN.positionOS;
                    posOS.xyz = floor(posOS.xyz * _VertexSnapping) / _VertexSnapping;
                    OUT.positionHCS = TransformObjectToHClip(posOS.xyz);
                    OUT.positionHCS.xy = floor(OUT.positionHCS.xy * _PixelSize) / _PixelSize;
                    OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                    return OUT;
                }

                half4 frag_main(Varyings IN) : SV_Target
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                }
            ENDHLSL
        }
    }
}

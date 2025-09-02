Shader "Custom/BetterOutlineObject"
{
    Properties
    {
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        [HDR]_OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth("Outline Width", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent"
            "Queue"="Transparent+1"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
        ENDHLSL

        // 1. Outline Pass
        Pass
        {
            Name "OutlinePass"
            Cull Front
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert_outline
            #pragma fragment frag_outline

            // Vertex attributes
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

                // Get object scale, for consistent outline thickness (works with non-uniform scales)
                float scale = unity_WorldTransformParams.w; // Use built-in scale parameter

                // Extrude position along normal
                float3 extrudedPos = IN.positionOS.xyz + normalize(IN.normalOS) * (_OutlineWidth / max(scale, 0.001));
                OUT.positionHCS = TransformObjectToHClip(extrudedPos);

                return OUT;
            }

            half4 frag_outline(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // 2. Main Pass
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
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag_main(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return texColor * _Color;
            }
            ENDHLSL
        }
    }
}

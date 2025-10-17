Shader "Custom/ObjectOutline"
{
    Properties
    {
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        [HDR]_OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth("Outline Width", Range(0, 0.1)) = 0.03
        _OutlineQuality("Outline Quality", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma multi_compile_instancing

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _OutlineQuality;
            CBUFFER_END
        ENDHLSL

        // Optimized Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            ZTest LEqual
            ZWrite On
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert_outline
            #pragma fragment frag_outline
            
            #pragma multi_compile_fog
            #pragma target 3.5

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half fogCoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Fast normalized object scale approximation
            half GetObjectScale()
            {
                float3 scale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21), 
                    length(unity_ObjectToWorld._m02_m12_m22)
                );
                // Use average scale for consistent outlines
                return (scale.x + scale.y + scale.z) * 0.333;
            }

            // Optimized vertex shader with quality levels
            Varyings vert_outline(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                half objectScale = GetObjectScale();
                half scaledWidth = _OutlineWidth / max(objectScale, 0.001);
                
                // Quality-based optimization
                half widthMultiplier = lerp(0.7, 1.0, _OutlineQuality);
                scaledWidth *= widthMultiplier;

                // Choose extrusion method based on quality
                float3 extrudedPos;
                if (_OutlineQuality > 0.5)
                {
                    // Higher quality: full normal extrusion
                    extrudedPos = IN.positionOS.xyz + IN.normalOS * scaledWidth;
                }
                else
                {
                    // Lower quality: normalized extrusion
                    extrudedPos = IN.positionOS.xyz + normalize(IN.normalOS) * scaledWidth;
                }

                OUT.positionHCS = TransformObjectToHClip(extrudedPos);
                
                // Fog support
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                
                return OUT;
            }

            // Ultra-simple fragment shader
            half4 frag_outline(Varyings IN) : SV_Target
            {
                half4 color = _OutlineColor;
                
                // Apply fog
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                
                return color;
            }
            ENDHLSL
        }

        // Optimized Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Back
            ZTest LEqual
            ZWrite On
            ColorMask RGB
            
            HLSLPROGRAM
            #pragma vertex vert_main
            #pragma fragment frag_main
            
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma target 3.5

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogCoord : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert_main(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                
                return OUT;
            }

            half4 frag_main(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 finalColor = texColor * _Color;
                
                // Apply fog
                finalColor.rgb = MixFog(finalColor.rgb, IN.fogCoord);
                
                return finalColor;
            }
            ENDHLSL
        }

        // Shadow Caster Pass for proper shadow reception
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    // Fallback for unsupported platforms
    FallBack "Universal Render Pipeline/Simple Lit"
}
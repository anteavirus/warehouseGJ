Shader "Custom/Patterns/Checkerboard"
{
    Properties
    {
        [Header(Pattern Settings)]
        _Scale ("Scale", Range(0.1, 50)) = 5.0
        [Toggle] _UseX ("Use X Axis", Float) = 1
        [Toggle] _UseY ("Use Y Axis", Float) = 1
        [Toggle] _UseZ ("Use Z Axis", Float) = 1
        
        [Header(Materials)]
        _Tex1 ("Texture 1", 2D) = "white" {}
        _Tex2 ("Texture 2", 2D) = "black" {}
        
        [Header(Color Fallback)]
        _Color1 ("Color 1", Color) = (1,1,1,1)
        _Color2 ("Color 2", Color) = (0,0,0,1)
        
        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        
        [Header(Tiling)]
        _Tiling("Tiling", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            // Properties
            float _Scale, _Smoothness, _Metallic;
            float _UseX, _UseY, _UseZ;
            float4 _Color1, _Color2;
            float4 _Tiling;
            
            TEXTURE2D(_Tex1);
            TEXTURE2D(_Tex2);
            SAMPLER(sampler_Tex1);
            SAMPLER(sampler_Tex2);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv * _Tiling.xy + _Tiling.zw;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Use world space coordinates for consistent pattern
                float3 worldPos = IN.positionWS;
                
                // Calculate which axes to use
                float3 useAxis = float3(_UseX, _UseY, _UseZ);
                if (dot(useAxis, 1) == 0)
                {
                    // Default to X and Z if no axes selected
                    useAxis = float3(1, 0, 1);
                }
                
                // Get the dominant normal to determine which plane we're on
                float3 absNormal = abs(normalize(IN.normalWS));
                float maxNormal = max(absNormal.x, max(absNormal.y, absNormal.z));
                
                // Select coordinates based on normal and enabled axes
                float2 gridPos;
                if (absNormal.y == maxNormal && useAxis.y > 0.5) 
                {
                    // Top/bottom - use XZ plane
                    gridPos = worldPos.xz;
                }
                else if (absNormal.x == maxNormal && useAxis.x > 0.5) 
                {
                    // Sides - use YZ plane  
                    gridPos = worldPos.yz;
                }
                else if (useAxis.z > 0.5)
                {
                    // Front/back - use XY plane
                    gridPos = worldPos.xy;
                }
                else
                {
                    // Fallback: use first available enabled axis
                    if (useAxis.x > 0.5 && useAxis.z > 0.5)
                        gridPos = worldPos.xz;
                    else if (useAxis.x > 0.5)
                        gridPos = worldPos.xy;
                    else if (useAxis.z > 0.5) 
                        gridPos = worldPos.yz;
                    else
                        gridPos = worldPos.xz; // Ultimate fallback
                }
                
                // FIXED: Create consistent checkerboard pattern with proper coordinate handling
                // The issue was that we need to handle negative coordinates properly
                float2 scaledPos = gridPos * _Scale;
                
                // Convert to integer grid coordinates, handling negatives correctly
                int2 gridCoord;
                gridCoord.x = (scaledPos.x >= 0) ? floor(scaledPos.x) : ceil(scaledPos.x - 1);
                gridCoord.y = (scaledPos.y >= 0) ? floor(scaledPos.y) : ceil(scaledPos.y - 1);
                
                // Create checkerboard pattern - FIXED: ensure it works with negative coordinates
                bool isChecker = ((gridCoord.x + gridCoord.y) & 1) == 0;
                
                // ALTERNATIVE FIX: Use this simpler approach that handles negatives automatically
                // float2 checkPos = floor(scaledPos + 1000.0); // Offset to ensure positive
                // bool isChecker = fmod(checkPos.x + checkPos.y, 2.0) < 1.0;
                
                half3 albedo;
                if (isChecker)
                {
                    half4 tex1 = SAMPLE_TEXTURE2D(_Tex1, sampler_Tex1, IN.uv);
                    albedo = (length(tex1.rgb - half3(1,1,1)) < 0.01) ? _Color1.rgb : tex1.rgb;
                }
                else
                {
                    half4 tex2 = SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, IN.uv);
                    albedo = (length(tex2.rgb) < 0.01) ? _Color2.rgb : tex2.rgb;
                }

                // Lighting
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                float NdotL = saturate(dot(IN.normalWS, mainLight.direction));
                float3 lighting = mainLight.color * (mainLight.shadowAttenuation * NdotL + 0.1);

                half4 color = half4(albedo * lighting, 1.0);
                color.rgb = MixFog(color.rgb, IN.positionHCS.z);
                
                return color;
            }
            ENDHLSL
        }
        
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
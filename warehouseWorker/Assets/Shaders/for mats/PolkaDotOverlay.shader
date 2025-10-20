Shader "Custom/Patterns/PolkaDotsLit"
{
    Properties
    {
        [Header(Pattern Settings)]
        _Scale ("Scale", Range(0.1, 50)) = 5.0
        _DotSize ("Dot Size", Range(0.05, 0.5)) = 0.2
        [Toggle] _UseXY ("Use XY Plane", Float) = 1
        [Toggle] _UseXZ ("Use XZ Plane", Float) = 1
        [Toggle] _UseYZ ("Use YZ Plane", Float) = 1
        
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
            float _Scale, _DotSize, _Smoothness, _Metallic;
            float _UseXY, _UseXZ, _UseYZ;
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
                float3 pos = IN.positionOS * _Scale;
                
                // Get the dominant normal to determine which plane we're on
                float3 absNormal = abs(normalize(IN.normalWS));
                float maxNormal = max(absNormal.x, max(absNormal.y, absNormal.z));
                
                // Select coordinates based on normal and enabled planes
                float2 gridPos;
                if (absNormal.y == maxNormal && _UseXZ > 0.5) 
                {
                    // Top/bottom - use XZ plane
                    gridPos = pos.xz;
                }
                else if (absNormal.x == maxNormal && _UseYZ > 0.5) 
                {
                    // Sides - use YZ plane  
                    gridPos = pos.yz;
                }
                else if (_UseXY > 0.5)
                {
                    // Front/back - use XY plane
                    gridPos = pos.xy;
                }
                else
                {
                    // Fallback: use first available enabled plane
                    if (_UseXZ > 0.5)
                        gridPos = pos.xz;
                    else if (_UseXY > 0.5)
                        gridPos = pos.xy;
                    else if (_UseYZ > 0.5) 
                        gridPos = pos.yz;
                    else
                        gridPos = pos.xz; // Ultimate fallback
                }
                
                // Create proper polka dot pattern
                float2 cell = floor(gridPos);
                float2 cellCenter = cell + 0.5;
                float2 localPos = gridPos - cellCenter;
                
                // Create dots in a grid pattern
                float dotPattern = step(length(localPos), _DotSize);
                
                // Alternative: use fractional coordinates for continuous dots
                // float2 tiledUV = frac(gridPos);
                // float dotPattern = step(length(tiledUV - 0.5), _DotSize);
                
                bool isDot = dotPattern > 0.5;
                
                half3 albedo;
                if (isDot)
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
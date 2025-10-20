Shader "Custom/ObjectGradientTransparent"
{
    Properties
    {
        _DarkColor("Dark Color", Color) = (0,0,0,1)
        _LightColor("Light Color", Color) = (1,1,1,0)
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(X,0,Y,1,Z,2)] _Axis("Gradient Axis", Int) = 0
        [Toggle] _Invert("Invert", Float) = 0
        _DepthPower("Depth Power", Range(0.1, 5)) = 1
        _DepthScale("Depth Scale", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionSS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _LightColor;
                float _GradientStart;
                float _GradientEnd;
                int _Axis;
                float _Invert;
                float _DepthPower;
                float _DepthScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionSS = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Axis-aligned gradient calculation
                float axisPos = IN.positionOS[_Axis];
                float gradient = saturate((axisPos - _GradientStart) / (_GradientEnd - _GradientStart));
                if (_Invert) gradient = 1.0 - gradient;

                // Improved depth calculation using view direction and normals
                float NdotV = 1 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float depthEffect = pow(NdotV * _DepthScale, _DepthPower);

                // Combine gradient with depth effect
                float combined = saturate(gradient * depthEffect);

                // Final color with proper depth-based alpha
                half4 color = lerp(_LightColor, _DarkColor, combined);
                color.a = combined; // Directly use combined effect for alpha

                return color;
            }
            ENDHLSL
        }
    }
}

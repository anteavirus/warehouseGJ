Shader "Custom/Pixelate"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {} // Required for cmd.Blit
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    float _PixelSize;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;
        return output;
    }

    float4 Pixelate(Varyings input) : SV_Target
    {
        // Calculate pixelated UVs
        float2 pixelatedUV = input.uv * _ScreenParams.xy / _PixelSize;
        pixelatedUV = floor(pixelatedUV);
        pixelatedUV = pixelatedUV * _PixelSize / _ScreenParams.xy;

        // Sample the texture
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pixelatedUV);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "PixelatePass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Pixelate
            ENDHLSL
        }
    }
}

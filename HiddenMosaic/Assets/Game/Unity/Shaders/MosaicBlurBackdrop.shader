Shader "HiddenMosaic/BlurBackdrop"
{
    Properties
    {
        _MainTex("Scene Capture", 2D) = "black" {}
        _BlurStrength("Blur Strength", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _BlurStrength;

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
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 SampleBlur(float2 uv)
            {
                float blur = max(_BlurStrength, 0.001) * 10.0;
                float2 d = _MainTex_TexelSize.xy * blur;
                half4 acc = 0;
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(-1, -1));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(0, -1));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(1, -1));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(-1, 0));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(1, 0));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(-1, 1));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(0, 1));
                acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d * float2(1, 1));
                return acc / 9.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return SampleBlur(input.uv);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

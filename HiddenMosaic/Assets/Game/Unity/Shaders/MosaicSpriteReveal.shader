Shader "HiddenMosaic/MosaicSpriteReveal"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        _RevealAmount("Aim Boost", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteReveal"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            float _RevealAmount;
            float4 _MosaicGlass;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.worldXY = world.xy;
                return o;
            }

            half LensReveal(float2 worldXY)
            {
                float2 delta = worldXY - _MosaicGlass.xy;
                float dist = length(delta);
                float radius = max(_MosaicGlass.z, 0.0001);
                float soft = max(_MosaicGlass.w, 0.0001);
                if (dist <= radius)
                    return 1.0;
                return 1.0 - smoothstep(0.0, soft, dist - radius);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 gray = half3(lum, lum, lum);
                half reveal = saturate(max(LensReveal(input.worldXY), _RevealAmount));
                c.rgb = lerp(gray, c.rgb, reveal);
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SpriteRevealFallback"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            float _RevealAmount;
            float4 _MosaicGlass;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.worldXY = world.xy;
                return o;
            }

            half LensReveal(float2 worldXY)
            {
                float2 delta = worldXY - _MosaicGlass.xy;
                float dist = length(delta);
                float radius = max(_MosaicGlass.z, 0.0001);
                float soft = max(_MosaicGlass.w, 0.0001);
                if (dist <= radius)
                    return 1.0;
                return 1.0 - smoothstep(0.0, soft, dist - radius);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 gray = half3(lum, lum, lum);
                half reveal = saturate(max(LensReveal(input.worldXY), _RevealAmount));
                c.rgb = lerp(gray, c.rgb, reveal);
                return c;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Stylized/Flat Prop"
{
    // Turns a generated PBR prop into flat poster colour.
    //
    // Rodin hands you a smooth mesh with a baked diffuse/metallic/roughness set. This
    // shader throws all of that away except the diffuse, and uses it only to decide WHICH
    // part of the prop a pixel belongs to - gold frame or purple orb - then paints that
    // part with two or three hand-picked flat colours. No lighting maths lives here on
    // purpose: the Stylized Relight feature adds the key, shadow and rim in screen space,
    // and the pixel filter supplies the grid. This pass only has to be flat and bold.
    Properties
    {
        [MainTexture] _BaseMap ("Generated diffuse", 2D) = "white" {}

        [Header(Gold)]
        _GoldLight ("Gold light", Color) = (1, 0.85, 0.38, 1)
        _GoldMid   ("Gold mid",   Color) = (0.86, 0.56, 0.17, 1)
        _GoldDark  ("Gold dark",  Color) = (0.48, 0.26, 0.12, 1)

        [Header(Orb)]
        _OrbLight ("Orb light", Color) = (0.80, 0.64, 1, 1)
        _OrbMid   ("Orb mid",   Color) = (0.48, 0.30, 0.92, 1)
        _OrbDark  ("Orb dark",  Color) = (0.22, 0.11, 0.46, 1)

        [Header(Zone split)]
        _OrbThreshold ("Purple detection", Range(0, 0.4)) = 0.04

        [Header(Tone steps)]
        _Black ("Input black", Range(0, 1)) = 0.12
        _White ("Input white", Range(0, 1)) = 0.78
        _Split ("Tone split", Range(0, 1)) = 0.5
        _SplitWidth ("Split spacing", Range(0.01, 0.5)) = 0.16

        [ToggleUI] _TwoToneOnly ("Two tones instead of three", Float) = 0
        [ToggleUI] _AlphaClip ("Alpha clipping", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "FlatProp"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _GoldLight, _GoldMid, _GoldDark;
                float4 _OrbLight, _OrbMid, _OrbDark;
                float _OrbThreshold;
                float _Black, _White, _Split, _SplitWidth;
                float _TwoToneOnly, _AlphaClip, _Cutoff;
            CBUFFER_END

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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float4 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (_AlphaClip > 0.5) clip(source.a - _Cutoff);

                float3 display = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    display = LinearToSRGB(display);
                #endif

                // Which part of the prop is this? Purple reads as blue dominating the
                // warm channels; everything else is treated as the gold frame.
                bool orb = (display.b - max(display.r, display.g)) > _OrbThreshold;

                // Normalise the baked shading into a usable 0..1 before stepping it,
                // so a dark or washed-out bake still lands on all the tones.
                float luma = dot(display, float3(0.2126, 0.7152, 0.0722));
                float level = saturate((luma - _Black) / max(0.01, _White - _Black));

                float3 light = orb ? _OrbLight.rgb : _GoldLight.rgb;
                float3 mid   = orb ? _OrbMid.rgb   : _GoldMid.rgb;
                float3 dark  = orb ? _OrbDark.rgb  : _GoldDark.rgb;

                float3 flat3;
                if (_TwoToneOnly > 0.5)
                {
                    flat3 = level >= _Split ? light : dark;
                }
                else
                {
                    float high = saturate(_Split + _SplitWidth);
                    float low  = saturate(_Split - _SplitWidth);
                    flat3 = level >= high ? light : (level >= low ? mid : dark);
                }

                #ifndef UNITY_COLORSPACE_GAMMA
                    flat3 = SRGBToLinear(flat3);
                #endif

                return float4(flat3, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

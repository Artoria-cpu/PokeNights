Shader "Pokenight/Character RedhornEyes"
{
    Properties
    {
        [MainTexture] _MainTex ("Pixel Texture", 2D) = "white" {}
        _GazeOffset ("Gaze offset in face coordinates", Vector) = (0,0,0,0)
        _EyeLeft ("Left eye opening", Vector) = (-0.068,1.587,-0.007,1.620)
        _EyeRight ("Right eye opening", Vector) = (0.016,1.587,0.080,1.620)
        [NoScaleOffset] _EyePupils ("Original pupil layer", 2D) = "black" {}
        [NoScaleOffset] _EyeOpening ("Fixed eye opening", 2D) = "black" {}
        _FaceRect ("Face projection bounds", Vector) = (-0.13,1.55,0.26,0.14)
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        [ToggleUI] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    // This block is ignored before compilation when URP is not installed.
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "10.0"
        }
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "RedhornUnlitURP"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#pragma target 3.5
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#include "Assets/CentralPark/Hyper3D/CharacterLighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EyePupils); SAMPLER(sampler_EyePupils);
            TEXTURE2D(_EyeOpening); SAMPLER(sampler_EyeOpening);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _AlphaClip;
                float _Cutoff;
                float4 _GazeOffset;
                float4 _EyeLeft;
                float4 _EyeRight;
                float4 _FaceRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 faceOS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
float3 parkWS:TEXCOORD7;
                float2 uv : TEXCOORD0;
                float3 faceOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.parkWS=TransformObjectToWorld(input.positionOS.xyz);
output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.faceOS = input.faceOS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                float3 p = input.faceOS;
                float2 eyeUV = (p.xy - _FaceRect.xy) / _FaceRect.zw;
                if (p.z > .015 && all(eyeUV > 0) && all(eyeUV < 1) && dot(_GazeOffset.xy,_GazeOffset.xy)>1e-12)
                {
                    float opening = SAMPLE_TEXTURE2D(_EyeOpening, sampler_EyeOpening, eyeUV).r;
                    float4 pupil = SAMPLE_TEXTURE2D(_EyePupils, sampler_EyePupils, eyeUV - _GazeOffset.xy / _FaceRect.zw);
                    float3 white = min(color.r,min(color.g,color.b)) > .75 ? color.rgb : float3(.90,.90,.94);
                    color.rgb = lerp(color.rgb, lerp(white,pupil.rgb,pupil.a),opening);
                }

                // A material-uniform branch avoids runtime keyword/variant dependencies.
                if (_AlphaClip > 0.5)
                    clip(color.a - _Cutoff);
                color.rgb=ParkCharacterLight(color.rgb,input.parkWS,input.positionCS);return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "RedhornUnlitBuiltIn"
            Tags { "LightMode" = "Always" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _EyePupils;
            sampler2D _EyeOpening;
            float4 _MainTex_ST;
            float4 _Color;
            float _AlphaClip;
            float _Cutoff;
                float4 _GazeOffset;
                float4 _EyeLeft;
                float4 _EyeRight;
                float4 _FaceRect;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 faceOS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 faceOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.faceOS = input.faceOS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv) * _Color;
                float3 p = input.faceOS;
                float2 eyeUV = (p.xy - _FaceRect.xy) / _FaceRect.zw;
                if (p.z > .015 && all(eyeUV > 0) && all(eyeUV < 1) && dot(_GazeOffset.xy,_GazeOffset.xy)>1e-12)
                {
                    float opening = tex2D(_EyeOpening, eyeUV).r;
                    float4 pupil = tex2D(_EyePupils, eyeUV - _GazeOffset.xy / _FaceRect.zw);
                    float3 white = min(color.r,min(color.g,color.b)) > .75 ? color.rgb : float3(.90,.90,.94);
                    color.rgb = lerp(color.rgb, lerp(white,pupil.rgb,pupil.a),opening);
                }

                if (_AlphaClip > 0.5)
                    clip(color.a - _Cutoff);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}

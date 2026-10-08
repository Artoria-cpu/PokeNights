Shader "Stylized/Enemy Eye Glow"
{
    // A small camera-facing red glow sitting on an eye. Billboarded in the vertex shader
    // from the camera basis, so nothing has to rotate the transform every frame.
    Properties
    {
        [HDR] _Colour ("Colour", Color) = (1, 0.1, 0.08, 1)
        _Intensity ("Intensity", Float) = 1
        _Size ("World size", Float) = 0.14
        _Power ("Falloff", Range(0.5, 8)) = 2.4
        _Core ("Core", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+190"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "EnemyEyeGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One      // additive
            ZWrite Off
            ZTest Always            // the glow reads through the head geometry
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Colour;
                float _Intensity, _Size, _Power, _Core;
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

                float3 centreWS = TransformObjectToWorld(float3(0, 0, 0));
                // View matrix rows are the camera's world-space basis vectors.
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up    = UNITY_MATRIX_V[1].xyz;

                float3 positionWS = centreWS + (right * input.positionOS.x + up * input.positionOS.y) * _Size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float falloff = pow(saturate(1.0 - radius), max(0.5, _Power));
                float core = saturate(1.0 - radius / max(0.01, _Core));
                float strength = saturate(falloff + core * core) * _Intensity;
                return float4(_Colour.rgb * strength, saturate(strength));
            }
            ENDHLSL
        }
    }

    Fallback Off
}

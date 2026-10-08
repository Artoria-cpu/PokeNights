Shader "Stylized/Enemy Eye Cross"
{
    // Two bands crossing the whole screen through a point - the anime "the boss just
    // locked on" flare. Drawn as one quad whose vertex shader writes clip space straight
    // from the mesh UV, so it always fills the view no matter where the object sits.
    //
    // It lives in the transparent queue rather than in a renderer feature, which means the
    // existing pixel filter still quantises it along with everything else.
    Properties
    {
        [HDR] _Colour ("Colour", Color) = (1, 0.15, 0.1, 1)
        _EyeWorld ("Eye world position", Vector) = (0, 1.6, 0, 1)
        _Intensity ("Intensity", Float) = 1
        _Thickness ("Bar thickness", Float) = 0.0022
        _Softness ("Bar softness", Float) = 0.003
        _Taper ("Taper towards the ends", Range(0, 1)) = 0.35
        _CoreSize ("Core size", Float) = 0.014
        _CoreBoost ("Core boost", Float) = 0.8
        _Aspect ("Aspect", Float) = 1.777
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "EnemyEyeCross"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One      // additive
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Colour;
                float4 _EyeWorld;
                float _Intensity, _Thickness, _Softness, _Taper, _CoreSize, _CoreBoost, _Aspect;
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
                float3 eye : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Ignore the transform entirely: the quad is the screen.
                output.positionCS = float4(input.uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
                output.uv = input.uv;
                float4 eyeCS = TransformWorldToHClip(_EyeWorld.xyz);
                output.eye = float3(eyeCS.xy / max(0.00001, eyeCS.w) * 0.5 + 0.5, eyeCS.w > 0 ? 1 : 0);
                return output;
            }

            float Band(float distance, float thickness, float softness)
            {
                return 1.0 - smoothstep(thickness * 0.5, thickness * 0.5 + max(0.0005, softness), distance);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float2 centre = input.eye.xy;
                if (input.eye.z < 0.5) return 0;      // the eye is behind the camera

                float2 delta = input.uv - centre;
                float dx = abs(delta.x) * _Aspect;  // measure both bars in screen heights
                float dy = abs(delta.y);

                float horizontal = Band(dy, _Thickness, _Softness);
                float vertical   = Band(dx, _Thickness, _Softness);

                // Gentle fade towards the far ends so the cross reads as a flare rather
                // than a pair of drawn lines. 0 keeps them at full strength edge to edge.
                float alongH = 1.0 - _Taper * saturate(dx / 1.2);
                float alongV = 1.0 - _Taper * saturate(dy / 0.9);

                float cross = horizontal * alongH + vertical * alongV;

                // A hot dot where the two meet.
                float radius = length(float2(dx, dy));
                float core = exp(-(radius * radius) / max(1e-5, _CoreSize * _CoreSize)) * _CoreBoost;

                float strength = saturate(cross + core) * _Intensity;
                return float4(_Colour.rgb * strength, saturate(strength));
            }
            ENDHLSL
        }
    }

    Fallback Off
}


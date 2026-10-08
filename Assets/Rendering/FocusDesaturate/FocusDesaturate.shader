Shader "Hidden/Stylized/FocusDesaturate"
{
    // Two passes working together:
    //   0 - stamps the subject's renderers into a mask, depth tested against the scene so
    //       only the parts the camera can actually see are marked.
    //   1 - drains the colour out of the world, softly: the mask edge is feathered and the
    //       effect ramps up with screen distance from the subject.
    //
    // Injected before the transparent queue, so particles, trails and slash ribbons are
    // drawn afterwards and keep their full colour.
    Properties
    {
        [HideInInspector] _FocusZTest ("Mask depth test", Float) = 4   // LessEqual
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Focus Subject Mask"
            ZWrite Off
            ZTest [_FocusZTest]
            Cull Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target { return half4(1, 0, 0, 0); }
            ENDHLSL
        }

        Pass
        {
            Name "Focus Desaturate"
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_FocusMask);
            float4 _FocusMask_TexelSize;

            float _FocusWeight;       // 0..1, how far into the effect we are
            float _FocusSaturation;   // colour left in the world at full effect; 0 = fully grey
            float4 _FocusTint;        // rgb tint of the grey, a = brightness
            float _FocusEdge;         // soft edge radius around the subject, in pixels
            float _FocusEdgeBias;     // lower values push the coloured area further out
            float4 _FocusCentre;      // xy = subject viewport position, z = 1 when valid
            float4 _FocusFalloff;     // x = start, y = end, z = amount, w = aspect

            static const float2 kRing[8] =
            {
                float2( 1.0000,  0.0000), float2( 0.7071,  0.7071),
                float2( 0.0000,  1.0000), float2(-0.7071,  0.7071),
                float2(-1.0000,  0.0000), float2(-0.7071, -0.7071),
                float2( 0.0000, -1.0000), float2( 0.7071, -0.7071)
            };

            float Mask(float2 uv)
            {
                float coverage = SAMPLE_TEXTURE2D_X(_FocusMask, sampler_LinearClamp, uv).r;

                float radius = max(0.0, _FocusEdge);
                if (radius > 0.001)
                {
                    // Two rings of taps turn the hard stencil into a real gradient.
                    float2 inner = _FocusMask_TexelSize.xy * radius * 0.55;
                    float2 outer = _FocusMask_TexelSize.xy * radius;
                    float sum = coverage, total = 1.0;

                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        sum += SAMPLE_TEXTURE2D_X(_FocusMask, sampler_LinearClamp, uv + kRing[i] * inner).r * 0.75;
                        sum += SAMPLE_TEXTURE2D_X(_FocusMask, sampler_LinearClamp, uv + kRing[i] * outer).r * 0.40;
                        total += 1.15;
                    }
                    coverage = sum / total;
                }

                float bias = saturate(_FocusEdgeBias);
                return saturate((coverage - bias) / max(0.02, 1.0 - bias));
            }

            // Colour survives near the subject and drains away towards the edges of frame.
            float Falloff(float2 uv)
            {
                float amount = saturate(_FocusFalloff.z);
                if (amount <= 0.001 || _FocusCentre.z < 0.5) return 1.0;

                float2 delta = uv - _FocusCentre.xy;
                delta.x *= _FocusFalloff.w;
                float ramp = smoothstep(_FocusFalloff.x, max(_FocusFalloff.x + 0.001, _FocusFalloff.y), length(delta));
                return lerp(1.0, ramp, amount);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);

                float amount = saturate(_FocusWeight) * (1.0 - Mask(uv)) * Falloff(uv);
                if (amount <= 0.002)
                    return source;

                float3 display = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    display = LinearToSRGB(display);
                #endif

                // Perceptual luma on display values keeps the greys from turning muddy.
                float luma = dot(display, float3(0.2126, 0.7152, 0.0722));
                float3 grey = luma * _FocusTint.rgb * _FocusTint.a;
                float3 drained = lerp(grey, display, saturate(_FocusSaturation));
                display = lerp(display, drained, amount);

                #ifndef UNITY_COLORSPACE_GAMMA
                    display = SRGBToLinear(display);
                #endif

                return float4(display, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

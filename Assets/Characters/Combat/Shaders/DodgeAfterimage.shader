Shader "Stylized/Dodge Afterimage"
{
    // A frozen copy of the character, painted one flat colour.
    //
    // It sits in the transparent queue on purpose: the Focus Desaturate pass runs at
    // BeforeRenderingTransparents, so these are drawn afterwards and keep their full
    // saturation while the rest of the world is grey. The pixel filter still runs last,
    // so they end up on the same grid as everything else.
    Properties
    {
        [HideInInspector] _GhostColour ("Colour", Vector) = (1, 1, 1, 0.45)
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _RimBoost ("Rim boost", Range(0, 2)) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 5    // SrcAlpha
        [HideInInspector] _DstBlend ("Dst blend", Float) = 10   // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DodgeAfterimage"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColour;   // rgb already in the working colour space, a = alpha
                float _RimPower;
                float _RimBoost;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = _WorldSpaceCameraPos - positionWS;
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float alpha = _GhostColour.a;

                // Optional edge lift so overlapping copies stay readable. Zero keeps it
                // a completely flat silhouette, which is the default.
                if (_RimBoost > 0.001)
                {
                    float3 normalWS = normalize(input.normalWS);
                    float3 viewWS = normalize(input.viewWS);
                    float fresnel = pow(saturate(1.0 - saturate(dot(normalWS, viewWS))), max(0.5, _RimPower));
                    alpha = saturate(alpha + fresnel * _RimBoost);
                }

                return float4(_GhostColour.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

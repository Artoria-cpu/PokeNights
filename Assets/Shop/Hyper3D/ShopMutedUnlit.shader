Shader "Pokenight/Shop Muted Pixel"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture",2D)="white"{}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _Saturation("Saturation",Range(0,1))=.55
        _Brightness("Brightness",Range(0,1.2))=.82
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit"}
        Pass
        {
            Name "ShopColor"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Saturation, _Brightness, _Cutoff;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half4 Frag(Varyings i):SV_Target
            {
                half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=LinearToSRGB(c);
                #endif
                half l=dot(c,half3(.2126,.7152,.0722));
                c=lerp(l.xxx,c,_Saturation)*_Brightness;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=SRGBToLinear(c);
                #endif
                return half4(c*_BaseColor.rgb,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}

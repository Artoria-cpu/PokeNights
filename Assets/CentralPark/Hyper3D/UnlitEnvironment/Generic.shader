Shader "Pokenight/Unlit Environment/Generic"
{
    Properties
    {
 _EnvironmentTint("Environment tint",Vector)=(1,1,1,1)
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = .5
        [ToggleUI] _AlphaClip("Alpha clipping", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _SrcBlend("Source", Float) = 1
        [HideInInspector] _DstBlend("Destination", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Source alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination alpha", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _AlphaToMask("Alpha mask", Float) = 0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
        Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _EnvironmentTint;
            float _AlphaClip, _Cutoff;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); return o; }
            half4 Frag(V i):SV_Target {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                if(_AlphaClip>.5) clip(c.a-_Cutoff);
                c.rgb *= _EnvironmentTint.rgb;
                return c;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
    Fallback Off
}

Shader "Stylized/Pixel Unlit With Shadows"
{
    Properties
    {
        _EnvironmentTint("Environment tint", Vector) = (1,1,1,1)
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
        UsePass "Pokenight/Unlit Environment/Generic/Unlit"
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
    Fallback Off
}

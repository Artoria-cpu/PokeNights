Shader "Character/NativeHitParticle"
{
 Properties { _MainTex("Single particle texture",2D)="white"{} _UVRect("Texture bounds",Vector)=(1,1,0,0) _Cells("Pixel detail",Float)=128 _Soft("Preserve opacity bands",Float)=0 _TextureColor("Use four colour flame palette",Float)=0 [Toggle] _RoundGlow("Round glow",Float)=0 _GlowCore("Solid glow radius",Range(0,0.95))=0.3 _DstBlend("Destination blend",Float)=10 }
 SubShader
 {
  Tags { "Queue"="Transparent+110" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha [_DstBlend] ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 position:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _UVRect;float _Cells;float _Soft;float _TextureColor;float _RoundGlow;float _GlowCore;
   CBUFFER_END
   V vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color;o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target
   {
    if(_RoundGlow>.5)
    {
     half4 glow=i.color;
     glow.a*=1-smoothstep(_GlowCore,1,length(i.uv-.5)*2);
     clip(glow.a-.001);return glow;
    }
    float2 uv=(floor(saturate(i.uv)*_Cells)+.5)/_Cells;
    half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv*_UVRect.xy+_UVRect.zw);
    half alpha=texel.a;
    alpha=_Soft>.5?floor(alpha*3+.5)/3:step(.40,alpha);
    half4 color=i.color;color.a*=alpha;
    // Discrete orange/gold/cream regions keep painted gradients out of the particle.
    if(_TextureColor>.5)
     color.rgb*=texel.g<.08?half3(.89,.033,.0015):texel.g<.48?half3(1,.171,.001):texel.g<.88?half3(1,.638,.003):half3(1,.955,.70);
    clip(color.a-.01);return color;
   }
   ENDHLSL
  }
 }
}

Shader "Character/FourSpikeParticle"
{
 Properties
 {
  _MainTex("User spike atlas",2D)="black"{}
  _UVRect("Single shape bounds",Vector)=(.5,.5,0,0)
  _Cells("Pixel detail",Float)=96
  _MaskThreshold("Remove black background",Range(0,1))=.25
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+120" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   struct V{float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _UVRect;float _Cells;float _MaskThreshold;
   CBUFFER_END
   V vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color;o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float2 uv=(floor(saturate(i.uv)*_Cells)+.5)/_Cells;
    half4 source=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv*_UVRect.xy+_UVRect.zw);
    clip(max(source.r,max(source.g,source.b))-_MaskThreshold);
    // Three solid colour regions. Native particle colour B drives white flash; R drives the final blackening.
    half3 warm=source.g<.08?half3(1,.06,.002):source.g<.48?half3(1,.32,.006):half3(1,.82,.025);
    half3 rgb=lerp(warm,half3(1,1,1),saturate(i.color.b))*i.color.r;
    return half4(rgb,source.a*i.color.a);
   }
   ENDHLSL
  }
 }
}

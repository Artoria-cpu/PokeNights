Shader "Weapons/OrbBeam"
{
 Properties
 {
  _Color("Tint",Color)=(1,.42,.035,1)
  [HDR] _HotColor("Hot core",Color)=(1.4,1.05,.48,1)
  [HideInInspector] _DstBlend("Destination blend",Float)=1
  _Intensity("Intensity",Range(0,12))=4
  _EdgePower("Edge softness",Range(.2,8))=2
  _Flow("Flow speed",Float)=5
  _NoiseScale("Noise scale",Float)=18
  _TailCut("Tail dissolve",Float)=0
  _EffectTime("Effect time",Float)=0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+120" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha [_DstBlend]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   float4 _Color,_HotColor; float _Intensity,_EdgePower,_Flow,_NoiseScale,_TailCut,_EffectTime;
   CBUFFER_END
   float hash(float n){return frac(sin(n)*43758.5453);}
   Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 frag(Varyings i):SV_Target
   {
    float2 uv=(floor(i.uv*float2(128,12))+.5)/float2(128,12);
    float across=saturate(1-abs(uv.y*2-1));
    float soft=pow(across,_EdgePower);
    float cell=floor((uv.x-(_Time.y+_EffectTime)*_Flow)*_NoiseScale);
    float energy=.78+.22*hash(cell);
    float pulse=.88+.12*sin((i.uv.x*_NoiseScale-_Time.y*_Flow)*6.28318);
    float dissolve=smoothstep(_TailCut-.035,_TailCut+.055,uv.x+.015*hash(cell));
    float alpha=floor(soft*energy*dissolve*8)/8*i.color.a*_Color.a;
    float3 hot=lerp(_Color.rgb,_HotColor.rgb,pow(across,5)*.65);
    return half4(hot*_Intensity*pulse*i.color.rgb,alpha);
   }
   ENDHLSL
  }
 }
}

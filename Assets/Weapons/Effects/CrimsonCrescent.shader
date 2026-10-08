Shader "Weapons/CrimsonCrescent"
{
 Properties { _Opacity("Opacity",Range(0,1))=1 _Age("Age",Float)=0 _PixelSize("Screen pixel block",Float)=4 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+115" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes {float4 position:POSITION;float2 uv:TEXCOORD0;float2 layer:TEXCOORD1;float4 color:COLOR;};
   struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float2 layer:TEXCOORD1;float4 color:COLOR;};
   CBUFFER_START(UnityPerMaterial)
   float _Opacity;float _Age;float _PixelSize;
   CBUFFER_END
   Varyings vert(Attributes i){Varyings o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.layer=i.layer;o.color=i.color;return o;}
   float3 Spectrum(float u)
   {
    float3 red=float3(1.30,.018,.045),purple=float3(.42,.028,.95),blue=float3(.018,.20,1.45);
    return lerp(lerp(red,purple,smoothstep(.20,.48,u)),blue,smoothstep(.55,.82,u))*.40;
   }
   half4 frag(Varyings i):SV_Target
   {
    // All three layers share pixel blocks; the halo fades through stepped brightness bands.
    float2 shift=(floor(i.position.xy/_PixelSize)+.5)*_PixelSize-i.position.xy;
    float2 uv=i.uv+ddx(i.uv)*shift.x+ddy(i.uv)*shift.y;
    uv=(floor(uv*float2(72,16))+.5)/float2(72,16);
    float u=uv.x,v=uv.y;clip(min(min(u,1-u),min(v-.10,.93-v)));
    if(i.layer.x>1.5)
    {
     float glow=floor(saturate(1-abs(v*2-1))*6)/6;
     glow*=glow;
     float ends=smoothstep(0,.12,u)*(1-smoothstep(.88,1,u));
     float3 color=Spectrum(u)*.65;
     float alpha=glow*ends*_Opacity*i.color.a;
     clip(alpha-.006);return half4(color,alpha);
    }
    if(i.layer.x>.5)
    {
     float core=step(.20,v)*(1-step(.84,v));
     float3 white=lerp(float3(1.5,1.5,1.55),float3(2.8,2.8,2.8),step(.42,v));
     float ends=step(.018,u)*(1-step(.985,u));
     float alpha=core*ends*_Opacity*i.color.a;
     clip(alpha-.006);return half4(white,alpha);
    }
    float cell=floor(u*72),noise=frac(sin(cell*91.73+floor(v*16)*17.13)*43758.54);
    float body=step(.19,v)*(1-step(.81,v));
    float rim=step(.70,v)*(1-step(.82,v));
    float glow=step(.12,v)*(1-step(.9,v))*.16;
    float late=smoothstep(.25,1,_Age);
    float3 inner=Spectrum(saturate(u+.07*late))*.50;
    float3 outer=Spectrum(u)*1.05;
    float3 fill=lerp(inner,outer,floor(saturate(v)*4)/3);
    float3 hot=Spectrum(u)*1.25;
    float fleck=step(.91,noise)*step(.30,v)*(1-step(.66,v));
    float3 accent=Spectrum(saturate(u+.12))*.65;
    float3 color=fill*body*(.76+.24*step(.4,noise))+hot*rim+accent*fleck+inner*glow;
    float ends=step(.018,u)*(1-step(.985,u));
    float alpha=saturate(body+rim+glow)*ends*_Opacity*i.color.a;
    clip(alpha-.006);return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}

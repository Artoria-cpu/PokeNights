Shader "Weapons/PrismaticEmber"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+116" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;return o;}
   half4 frag(V i):SV_Target
   {
    float2 uv=(floor(i.uv*8)+.5)/8;float2 d=abs(uv*2-1);
    float coverage=step(max(d.x,d.y),.9)*step(d.x+d.y,1.4);
    float core=step(max(d.x,d.y),.26);float border=step(.67,max(d.x,d.y));
    float3 color=i.c.rgb*lerp(1.65,.50,border)+core*float3(.65,.45,.22);
    clip(coverage*i.c.a-.01);return half4(color,coverage*i.c.a);
   }
   ENDHLSL
  }
 }
}

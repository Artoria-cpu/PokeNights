Shader "Weapons/OrbBeamWind"
{
 Properties { _Color("Air and dust tint",Color)=(.72,.66,.54,1) _EffectTime("Effect time",Float)=0 _Style("0 Ring wall 1 Beam streaks",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+122" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   float4 _Color;float _EffectTime,_Style;
   CBUFFER_END
   Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 frag(Varyings i):SV_Target
   {
    float2 uv=(floor(i.uv*float2(96,10))+.5)/float2(96,10);
    float flow=uv.x-_EffectTime*.42;
    float feather=smoothstep(0,.14,uv.y)*(1-smoothstep(.65,1,uv.y));
    float torn=smoothstep(-.65,-.05,sin(flow*19)+.27*sin(flow*47+uv.y*5));
    if(_Style<.5)torn=.32+.68*torn;
    else
    {
     float slash=frac(flow*4+uv.y*.43);
     torn=step(.16,slash)*(1-step(.92,slash));
     feather=step(.05,uv.y)*(1-step(.96,uv.y));
    }
    float streak=floor((.84+.16*sin(uv.y*15+flow*11))*5)/5;
    return half4(_Color.rgb*i.color.rgb*streak,floor(feather*torn*6)/6*i.color.a*_Color.a);
   }
   ENDHLSL
  }
 }
}

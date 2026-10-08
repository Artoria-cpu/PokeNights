Shader "Weapons/OrbBeamSprite"
{
 Properties
 {
  [HDR] _Color("Gold",Color)=(1,.38,.025,1)
  [HDR] _HotColor("Hot core",Color)=(1.8,1.45,.72,1)
  _Intensity("Intensity",Float)=2
  _Opacity("Opacity",Float)=1
  _Shape("0 Sphere 1 Aura 2 Cross",Float)=0
  _Spin("Spin",Float)=0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+121" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
   float4 _Color,_HotColor; float _Intensity,_Opacity,_Shape,_Spin;
   CBUFFER_END
   Varyings vert(Attributes i)
   {
    Varyings o;
    float size=length(float3(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0]));
    float3 center=TransformObjectToWorld(float3(0,0,0));
    float3 cameraRight=UNITY_MATRIX_I_V._m00_m10_m20;
    float3 cameraUp=UNITY_MATRIX_I_V._m01_m11_m21;
    o.positionCS=TransformWorldToHClip(center+(cameraRight*i.positionOS.x+cameraUp*i.positionOS.y)*size);
    o.uv=i.uv;return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    float grid=_Shape<.5?40:(_Shape<1.5?64:192);
    float2 p=((floor(i.uv*grid)+.5)/grid)*2-1;float c=cos(_Spin),s=sin(_Spin);p=mul(float2x2(c,-s,s,c),p);
    float r=length(p),mask,hot;
    if(_Shape<.5)
    {
     mask=1-smoothstep(.60,1,r);hot=1-smoothstep(.05,.83,r);
    }
    else if(_Shape<1.5)
    {
     mask=exp(-r*r*5.2)*pow(saturate(1-r),1.3);hot=pow(saturate(1-r*2.2),3)*.28;
    }
    else
    {
     float2 a=abs(p);
     float arm=max(a.x,a.y),side=min(a.x,a.y);
     float width=.034*pow(saturate(1-arm),2)+.002;
     mask=(1-step(width,side))*(1-step(.96,arm));
     hot=mask*(.55+.45*(1-step(width*.42,side)));
    }
    return half4(lerp(_Color.rgb,_HotColor.rgb,floor(hot*6)/6)*_Intensity*(_Shape>1.5?.55:1),floor(mask*12)/12*_Opacity*_Color.a);
   }
   ENDHLSL
  }
 }
}

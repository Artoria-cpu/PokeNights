Shader "Weapons/Orb Weapon Two Tone"
{
 Properties
 {
  _BaseMap("Color Regions",2D)="white"{}
  _Blue("Blue",Color)=(.025,.22,.94,1)
  _BlueShadow("Blue Shadow",Color)=(.015,.045,.38,1)
  _Silver("Silver",Color)=(.84,.85,.88,1)
  _SilverShadow("Silver Shadow",Color)=(.39,.42,.49,1)
  _PixelResolution("Texture Pixel Resolution",Float)=256
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_Blue,_BlueShadow,_Silver,_SilverShadow;
   float _PixelResolution;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;};
   V Vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.normalWS=TransformObjectToWorldNormal(a.normalOS);v.uv=TRANSFORM_TEX(a.uv,_BaseMap);return v;}
   half4 Frag(V v):SV_Target
   {
    float resolution=max(16,_PixelResolution);
    float2 uv=(floor(v.uv*resolution)+.5)/resolution;
    half3 region=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
    half blue=step(region.r*1.2+.025,region.b)*step(region.g*1.08,region.b);
    half lit=step(.12,dot(normalize(v.normalWS),normalize(float3(-.45,.8,-.5))));
    return lerp(lerp(_SilverShadow,_Silver,lit),lerp(_BlueShadow,_Blue,lit),blue);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

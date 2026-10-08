Shader "Character/TargetOutline" {
 Properties{_Color("Outline",Color)=(1,.72,.12,1) _Expansion("Expansion",Float)=1.045 _OutlineWidth("Normal expansion",Float)=0}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10"}Pass{Cull Front ZWrite Off ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Color;float _Expansion;float _OutlineWidth;
 float4 vert(float4 p:POSITION,float3 normal:NORMAL):SV_POSITION{
  float3 world=TransformObjectToWorld(p.xyz*_Expansion);
  world+=TransformObjectToWorldNormal(normal)*_OutlineWidth;
  return TransformWorldToHClip(world);
 }
 half4 frag():SV_Target{return _Color;}
 ENDHLSL
 }}}

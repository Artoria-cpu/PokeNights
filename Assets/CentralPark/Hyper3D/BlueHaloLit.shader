Shader "Pokenight/Character BlueHalo"
{
 Properties { [MainTexture] _BaseMap("Original texture",2D)="white"{} _EyePupils("Pupils",2D)="black"{} _EyeOpening("Eye opening",2D)="black"{} _EyeBackdrop("Sclera",2D)="white"{} _GazeOffset("Gaze",Vector)=(0,0,0,0) _PupilCentering("Original pupil centering",Vector)=(0,0,0,0) _FaceRect("Face bounds",Vector)=(-.17,1.47,.34,.24) }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 Tags {"LightMode"="UniversalForwardOnly"} Cull Back ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#pragma target 3.5
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#include "Assets/CentralPark/Hyper3D/CharacterLighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_EyePupils);SAMPLER(sampler_EyePupils);TEXTURE2D(_EyeOpening);SAMPLER(sampler_EyeOpening);TEXTURE2D(_EyeBackdrop);SAMPLER(sampler_EyeBackdrop);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;float4 _GazeOffset;float4 _FaceRect;float4 _PupilCentering;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float3 rest:TEXCOORD1;float2 backing:TEXCOORD2;};
 struct V{float4 p:SV_POSITION;float3 parkWS:TEXCOORD7;float2 uv:TEXCOORD0;float3 rest:TEXCOORD1;float2 backing:TEXCOORD2;};
 V Vert(A i){V o;o.parkWS=TransformObjectToWorld(i.p.xyz);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.rest=i.rest;o.backing=i.backing;return o;}
 half4 Frag(V i):SV_Target{
 half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);if(i.backing.x>.5){float shadow=smoothstep(1.29,1.275,i.rest.y)*smoothstep(1.195,1.215,i.rest.y);c.rgb=lerp(float3(.96,.98,1),float3(.46,.55,.98),shadow*.72);}float2 p=(i.rest.xy-_FaceRect.xy)/_FaceRect.zw;
 if(i.rest.z>.10&&all(p>0)&&all(p<1)&&(dot(_GazeOffset.xy,_GazeOffset.xy)+dot(_PupilCentering.xy,_PupilCentering.xy))>1e-12){
 half mask=SAMPLE_TEXTURE2D(_EyeOpening,sampler_EyeOpening,p).r;
 half4 pupil=SAMPLE_TEXTURE2D(_EyePupils,sampler_EyePupils,p-(_GazeOffset.xy+float2(i.rest.x<0?_PupilCentering.x:_PupilCentering.y,0))/_FaceRect.zw);
 half3 white=SAMPLE_TEXTURE2D(_EyeBackdrop,sampler_EyeBackdrop,p).rgb;
 c.rgb=lerp(c.rgb,lerp(white,pupil.rgb,pupil.a),mask);
 }c.rgb=ParkCharacterLight(c.rgb,i.parkWS,i.p);return c;}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}



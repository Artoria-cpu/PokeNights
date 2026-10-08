Shader "Pokenight/BloodStain" {
Properties { _Color("Blood",Color)=(.32,.008,.018,1) _Wash("Wash",Range(0,1))=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" "RenderType"="TransparentCutout" }
Pass { ZWrite Off Offset -1,-1 Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
CBUFFER_START(UnityPerMaterial)
float4 _Color; float _Wash;
CBUFFER_END
V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
half4 frag(V i):SV_Target {float a=atan2(i.uv.y,i.uv.x);float edge=.68+.15*sin(a*9)+.09*sin(a*17+2);float r=length(i.uv);float splash=step(r,edge);float2 cell=floor(i.uv*8);float rnd=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453);splash=max(splash,step(.91,rnd)*step(length(frac(i.uv*8)-.5),.2)*step(r,1.25));clip(splash-.5);float washNoise=frac(sin(dot(floor(i.uv*50),float2(19.19,31.37)))*43758.5453);clip(washNoise-_Wash);return half4(_Color.rgb*(.75+.25*saturate(1-r)),1);}
ENDHLSL
} }
}


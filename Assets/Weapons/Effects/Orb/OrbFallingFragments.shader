Shader "Weapons/OrbFallingFragments"
{
 Properties { _BaseColor("Black",Color)=(.012,.008,.02,1) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;
 CBUFFER_END
 struct Attributes{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
 struct Varyings{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
 Varyings vert(Attributes v){Varyings o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;o.color=v.color;return o;}
 half4 frag(Varyings i):SV_Target {
 float2 q=i.uv*2-1;float cells=frac(sin(dot(floor(i.uv*12),float2(12.9898,78.233)))*43758.5453);
 float blob=1-dot(q,q)+cells*.3;clip(blob-.3);clip(cells-(1-i.color.a)*.92);
 return half4(_BaseColor.rgb,saturate(blob*4)*saturate(i.color.a*2));
 }
 ENDHLSL
 }
 }
}

Shader "Weapons/OrbDissolvingShell"
{
 Properties { _Coverage("Coverage", Range(0,1))=1 _Phase("Phase",Float)=0 _Inflate("Shell thickness",Float)=0.025 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+20" }
 Pass {
 Cull Off ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _Coverage,_Phase,_Inflate;
 CBUFFER_END
 struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct Varyings {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float3 world:TEXCOORD2;};
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);}
 Varyings vert(Attributes v){Varyings o;float rough=noise(v.positionOS.xyz*25+_Phase);float3 p=v.positionOS.xyz+v.normalOS*(_Inflate*(.6+rough));o.positionCS=TransformObjectToHClip(p);o.world=TransformObjectToWorld(p);o.p=v.positionOS.xyz;o.n=TransformObjectToWorldNormal(v.normalOS);return o;}
 half4 frag(Varyings i):SV_Target {
 float n=noise(i.p*22+float3(_Phase*1.4,_Phase*2.6,0))*.7+noise(i.p*49-_Phase*3)*.3;
 float threshold=lerp(1.05,.18,_Coverage);clip(n-threshold);
 float edge=1-smoothstep(0,.085,n-threshold);
 float rim=pow(1-saturate(abs(dot(normalize(i.n),normalize(GetWorldSpaceViewDir(i.world))))),3);
 return half4(half3(.008,.006,.014)+edge*half3(.035,.023,.055)+rim*half3(.025,.018,.045),1);
 }
 ENDHLSL
 }
 }
}

Shader "Character/PunchImpact" {
 Properties { _Tint("Tint", Color)=(1,1,1,1) _Glow("Soft glow",Float)=0 _Hole("Expanding center",Float)=0 }
 SubShader { Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint;float _Glow;float _Hole;
 CBUFFER_END
 Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
 half4 frag(Varyings i):SV_Target {half4 c=_Tint;if(_Glow>.5){float r=length(i.uv*2-1);float outer=pow(saturate(1-r),1.35);float inner=_Hole<.001?1:smoothstep(_Hole-.1,_Hole+.04,r);c.a*=outer*inner*2;}return c;}
 ENDHLSL
 } }
}

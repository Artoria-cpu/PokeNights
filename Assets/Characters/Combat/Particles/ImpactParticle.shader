Shader "Character/ImpactParticle" {
 Properties { _MainTex("Atlas",2D)="white"{} _Tile("Tile scale and offset",Vector)=(.5,.5,0,.5) _Soft("Radial falloff",Float)=0 _ClearCenter("Clear center",Float)=1 _Age("Effect age",Float)=0 _Brightness("Brightness",Float)=1 _DstBlend("Destination blend",Float)=10 _PixelGrid("Pixel cells per sprite",Float)=48 _Changing("Use strike color curve",Float)=0 _PhaseColor("Lifetime color",Color)=(1,1,1,1) _PhaseAlpha("Lifetime opacity",Float)=1 }
 SubShader {Tags{"Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Pass {Blend SrcAlpha [_DstBlend] ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _Tile;float _Soft;float _ClearCenter;float _Age;float _Brightness;float _PixelGrid;float _Changing;float4 _PhaseColor;float _PhaseAlpha;
 CBUFFER_END
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{
 float2 uv=(floor(saturate(i.uv)*_PixelGrid)+.5)/_PixelGrid;
 half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv*_Tile.xy+_Tile.zw);
 half4 c=i.c;c.rgb*=_Brightness*lerp(float3(1,1,1),_PhaseColor.rgb,_Changing);c.a*=lerp(1,_PhaseAlpha,_Changing);
 float coverage=floor(saturate(tex.a)*5+.5)/5;
 float r=length(uv*2-1);if(_Soft>.5)coverage*=floor(pow(saturate(1-r),1.1)*5+.5)/5;
 if(_ClearCenter>.5&&_Age>.34){float t=smoothstep(.34,1,_Age);coverage*=step(t*.82,r);}
 c.a*=coverage;clip(c.a-.002);return c;
 }

 ENDHLSL
 }} }

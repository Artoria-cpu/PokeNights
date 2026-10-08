Shader "Character/LimbTrail" {
 Properties { _PixelSize("Screen pixel block",Float)=3 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+120" "RenderType"="Transparent"}Pass{Cull Off ZWrite Off Blend SrcAlpha One
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
 CBUFFER_START(UnityPerMaterial)
 float _PixelSize;
 CBUFFER_END
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{
 // Evaluate the ribbon at a shared pixel-cell center. The wider geometry leaves room for square edge steps.
 float2 shift=(floor(i.p.xy/_PixelSize)+.5)*_PixelSize-i.p.xy;
 float2 uv=i.uv+ddx(i.uv)*shift.x+ddy(i.uv)*shift.y;
 half4 c=i.c+ddx(i.c)*shift.x+ddy(i.c)*shift.y;
 float edge=abs(uv.y*2-1);clip(.65-edge);
 float bands=ceil(saturate(1-edge/.65)*3)/3;
 c.rgb*=1.8;c.a*=bands;return c;
 }
 ENDHLSL
 }}}

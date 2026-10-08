Shader "Character/LayeredImpact"
{
 Properties { _Glow("Glow layer",Float)=0 _DstBlend("Destination blend",Float)=10 _PixelSize("Screen pixel step",Float)=2 }
 SubShader
 {
  Tags { "Queue"="Transparent+110" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha [_DstBlend] ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 position:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 shape:TEXCOORD1; };
   struct Varyings { float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float shape:TEXCOORD1; };
   CBUFFER_START(UnityPerMaterial)
   float _Glow;float _PixelSize;
   CBUFFER_END
   Varyings vert(Attributes i){Varyings o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color;o.uv=i.uv;o.shape=i.shape.x;return o;}
   half4 frag(Varyings i):SV_Target
   {
    half4 color=i.color;
    float pixel=max(1,_PixelSize);
    float2 shift=(floor(i.position.xy/pixel)+.5)*pixel-i.position.xy;
    float2 uv=i.uv+ddx(i.uv)*shift.x+ddy(i.uv)*shift.y;
    clip(min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
    if(_Glow>.5)
    {
     // Three flat opacity bands; no smooth gradient or outline.
     float r=length(uv*2-1);clip(.96-r);
     color.a*=r<.38?1:r<.68?.55:.22;
    }
    else if(i.shape>.5)
    {
     float halfWidth=saturate(min(uv.x/.20,(1-uv.x)/.80));
     clip(halfWidth-abs(uv.y*2-1));
    }
    clip(color.a-.003);return color;
   }
   ENDHLSL
  }
 }
}

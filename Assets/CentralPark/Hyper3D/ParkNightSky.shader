Shader "Pokenight/Park Night Sky"
{
 Properties { _Zenith("Zenith",Color)=(0.008,0.015,0.045,1) _Horizon("Horizon",Color)=(0.055,0.075,0.13,1) }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Zenith,_Horizon;
   struct Out { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
   Out vert(float4 vertex:POSITION){Out o;o.position=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o;}
   half4 frag(Out i):SV_Target {
    float3 d=normalize(i.direction);float h=saturate(d.y);
    float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.45));
    float2 uv=float2(atan2(d.z,d.x)/6.2831853+.5,asin(d.y)/3.14159265+.5)*float2(580,290);
    float2 cell=floor(uv);float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
    float star=step(.996,seed)*step(length(frac(uv)-.5),.14)*smoothstep(0,.2,h);
    return half4(sky+star*float3(.5,.6,.8),1);
   }
   ENDHLSL
  }
 }
}

Shader "Pokenight/Unlit Environment/TealWitch"
{
 Properties
 {
 _EnvironmentTint("Environment tint",Vector)=(1,1,1,1)
  _BaseMap("Original Texture",2D)="white"{}
  _Part("Part: body 0, hair 1, hat 2, eyes 3",Float)=0
  _Warmth("Shadow warmth",Range(0,1))=1
  _Solid("Use solid vertex colors",Float)=0
  _EyePupils("Painted pupils",2D)="black"{}
  _EyeOpening("Fixed eye opening",2D)="black"{}
  _EyeBackdrop("Sclera",2D)="white"{}
  _GazeOffset("Gaze",Vector)=(0,0,0,0)
  _FaceRect("Face bounds",Vector)=(-.12,1.455,.24,.105)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Cull Off
  Pass
  {
   Name "Unlit"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_EyePupils); SAMPLER(sampler_EyePupils);
   TEXTURE2D(_EyeOpening); SAMPLER(sampler_EyeOpening);
   TEXTURE2D(_EyeBackdrop); SAMPLER(sampler_EyeBackdrop);
   CBUFFER_START(UnityPerMaterial)
 float4 _EnvironmentTint;
   float4 _BaseMap_ST;
   float _Part, _Warmth, _Solid;
   float4 _GazeOffset, _FaceRect;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;float3 reference:TEXCOORD1;float3 restNormal:TEXCOORD2;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 p:TEXCOORD1;float3 n:TEXCOORD2;float4 color:COLOR;};
   Varyings vert(Attributes a)
   {
    Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.p=a.reference;o.n=a.restNormal;o.color=a.color;return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    float3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    float2 eyeUV=(i.p.xy-_FaceRect.xy)/_FaceRect.zw;
    if((_Part<.5 || _Part>2.5) && _Solid<.5 && i.p.z>.045 && all(eyeUV>0) && all(eyeUV<1) && dot(_GazeOffset.xy,_GazeOffset.xy)>1e-12)
    {
     float opening=SAMPLE_TEXTURE2D(_EyeOpening,sampler_EyeOpening,eyeUV).r;
     float4 pupil=SAMPLE_TEXTURE2D(_EyePupils,sampler_EyePupils,eyeUV-_GazeOffset.xy/_FaceRect.zw);
     float3 sclera=SAMPLE_TEXTURE2D(_EyeBackdrop,sampler_EyeBackdrop,eyeUV).rgb;
     original=lerp(original,lerp(sclera,pupil.rgb,pupil.a),opening);
    }
    float3 c=LinearToSRGB(original);
    if(_Solid>.5)c=i.color.rgb;
    float3 altered=c;
    float3 p=i.p;float3 n=normalize(i.n);
    if(_Part>.5 && _Part<1.5)
    {
     float inner=step(.10,c.b-c.g)*step(c.g,.53);
     altered=lerp(float3(.19,.59,.65),float3(.13,.42,.51),inner);
     float ax=abs(p.x);
     // Fixed geometric boundaries, hard steps and one solid colour per shadow region.
     float tip=step(p.y,1.035+.35*(ax-.15))*step(.125,ax);
     float curl=step(.42,-n.y)*step(.14,ax)*step(p.y,1.42);
     float shadow=max(tip,curl);
     altered=lerp(altered,float3(.48,.20,.35),shadow);
    }
    else if(_Part>1.5 && _Part<2.5)
    {
     float band=step(.20,c.r)*step(c.b,c.r*1.65);
     float gold=step(.65,c.r)*step(.40,c.g);
     altered=lerp(float3(.16,.10,.25),float3(.35,.16,.32),band);
     altered=lerp(altered,float3(.97,.67,.36),gold);
     float underside=step(n.y,-.16)*step(p.y,1.68)*(1-gold);
     altered=lerp(altered,float3(.54,.20,.32),underside);
    }
    else if(_Part>4.5)
    {
     // Opaque briefs, one clean burgundy shadow and a narrow waistband.
     altered=float3(.20,.10,.26);
     if(p.y>.966)altered=float3(.36,.17,.32);
    }
    else if(_Part>3.5)
    {
     float stripe=step(.29,c.r);
     float trim=step(.65,c.r)*step(.38,c.g)*step(c.b,c.g*.85);
     altered=lerp(float3(.24,.12,.30),float3(.46,.25,.43),stripe);
     altered=lerp(altered,float3(.88,.59,.32),trim);
     if(p.z<-.025){
      // Shared rest-space design hides the old hair/skirt separation patch.
      float pleat=step(.40,abs(p.x)/max(.025,1.22-p.y))*step(abs(p.x)/max(.025,1.22-p.y),.70)*step(p.y,1.065);
      altered=lerp(float3(.24,.12,.30),float3(.46,.25,.43),pleat);
      float hem=step(.945,p.y)*step(p.y,.957);
      altered=lerp(altered,float3(.88,.59,.32),hem);
     }
    }
    else if(_Part<.5 && p.y<1.40)
    {
     float value=max(c.r,max(c.g,c.b));
     float legSkin=(_Part<.5 && p.y>.205 && p.y<.978 && (p.x<0 || p.y>.797))?1:0;
     if(legSkin>.5){float knee=step(abs(p.y-.52)/.085+abs(p.x+.106)/.038,1);float sideShade=step(.72,abs(n.x))*step(p.y,.79);altered=lerp(float3(1,.81,.72),float3(.98,.48,.53),max(knee,sideShade));}
     else if(value<.62){float stripe=step(.29,c.r);altered=lerp(float3(.24,.12,.30),float3(.46,.25,.43),stripe);}
     else if(c.r>.70 && c.g>.42 && c.b<c.g*.80){altered=float3(.88,.59,.32);}
     else if(_Part<.5 && p.y>1.12 && p.y<1.375 && abs(p.x)<.505){float shade=step(.3,-n.y);float ax=abs(p.x);float fold=step(abs(p.y-(1.27+.23*(ax-.33))),.004)*step(.25,ax)*step(ax,.46);altered=lerp(float3(1,.94,.85),float3(.84,.53,.65),max(shade,fold));}
     else if(c.r-c.g>.13){altered=c.g<.65?float3(.98,.48,.53):float3(1,.81,.72);}
     else {float shade=step(.03,c.b-c.r);altered=lerp(float3(1,.94,.85),float3(.84,.53,.65),shade);
      float ax=abs(p.x);float sleeve=step(.22,ax)*step(ax,.47)*step(1.19,p.y)*step(p.y,1.35);
      float fold1=step(abs(p.y-(1.27+.23*(ax-.33))),.004);
      float fold2=step(abs(p.y-(1.29-.35*(ax-.41))),.003);
      altered=lerp(altered,float3(.84,.53,.65),sleeve*max(fold1,fold2));
     }
    }
    return half4(SRGBToLinear(_Warmth>.5?altered:c)*_EnvironmentTint.rgb,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}


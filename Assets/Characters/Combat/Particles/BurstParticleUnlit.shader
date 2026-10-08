Shader "Character/BurstParticleUnlit"
{
 Properties
 {
  [MainTexture] _BaseMap("Particle texture",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  [HideInInspector] _Cutoff("Alpha cutoff",Range(0,1))=0
  [HideInInspector] _BumpMap("Normal",2D)="bump"{}
  [HideInInspector] _EmissionMap("Emission",2D)="white"{}
  [HideInInspector] _EmissionColor("Emission",Color)=(0,0,0,0)
  [HideInInspector] _Surface("Surface",Float)=1
  [HideInInspector] _BaseColorAddSubDiff("Colour blend",Vector)=(0,0,0,0)
  [HideInInspector] _SoftParticleFadeParams("Soft fade",Vector)=(0,0,0,0)
  [HideInInspector] _CameraFadeParams("Camera fade",Vector)=(0,0,0,0)
  [HideInInspector] _DistortionStrengthScaled("Distortion",Float)=0
  [HideInInspector] _DistortionBlend("Distortion blend",Float)=0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+120" }
  Pass
  {
   Name "ParticleForward"
   Tags { "LightMode"="UniversalForward" }
   Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma never_use_dxc
   #pragma vertex BurstVertex
   #pragma fragment fragParticleUnlit
   #pragma multi_compile_instancing
   #pragma instancing_options procedural:ParticleInstancingSetup
   #define _SURFACE_TYPE_TRANSPARENT 1
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitForwardPass.hlsl"
   // Use URP's particle transform, colour, UV and alpha path. Crop padding with the material's tiling/offset.
   VaryingsParticle BurstVertex(AttributesParticle input)
   {
    VaryingsParticle output=vertParticleUnlit(input);
    output.texcoord=(floor(saturate(output.texcoord)*128)+.5)/128;
    output.texcoord=output.texcoord*_BaseMap_ST.xy+_BaseMap_ST.zw;
    return output;
   }
   ENDHLSL
  }
 }
}

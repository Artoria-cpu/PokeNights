Shader "Character/ParryFlashAdditive"
{
 Properties
 {
  [MainTexture] _BaseMap("Particle texture",2D)="white"{}
  [HDR][MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  // 默认 LessEqual（4）：会被角色身体挡住。碰撞点在两人身体之间，想让光压在身体上面就改成 Always（8）。
  [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest",Float)=4
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
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+130" }
  Pass
  {
   Name "ParryFlashForward"
   Tags { "LightMode"="UniversalForward" }
   // Additive: alpha drives how much light the particle adds, so HDR tints bloom instead of occluding.
   Blend SrcAlpha One, One One
   ZWrite Off Cull Off
   ZTest [_ZTest]
   HLSLPROGRAM
   #pragma target 4.5
   #pragma never_use_dxc
   #pragma vertex vertParticleUnlit
   #pragma fragment fragParticleUnlit
   #pragma multi_compile_instancing
   #pragma instancing_options procedural:ParticleInstancingSetup
   #define _SURFACE_TYPE_TRANSPARENT 1
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitForwardPass.hlsl"
   ENDHLSL
  }
 }
 Fallback "Universal Render Pipeline/Particles/Unlit"
}

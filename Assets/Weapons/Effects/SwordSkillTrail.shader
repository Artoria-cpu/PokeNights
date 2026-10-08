Shader "Weapons/Sword Skill Trail"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 position : POSITION; half4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; half4 color : COLOR; };
            Varyings vert(Attributes input) { Varyings o; o.position=TransformObjectToHClip(input.position.xyz); o.color=input.color; return o; }
            half4 frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}

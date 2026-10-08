Shader "Hidden/Redhorn/PixelFilter"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _ColourLevels;
            float _ColourStrength;
            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 colour = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,input.texcoord,0);
                float3 display = colour.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                display = LinearToSRGB(display);
                #endif
                float steps = max(1,_ColourLevels-1);
                float3 grouped = floor(saturate(display)*steps+.5)/steps;
                display = lerp(display,grouped,_ColourStrength);
                #ifndef UNITY_COLORSPACE_GAMMA
                display = SRGBToLinear(display);
                #endif
                return float4(display,colour.a);
            }
            ENDHLSL
        }
    }
}

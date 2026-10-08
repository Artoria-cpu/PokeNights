#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
half3 ParkCharacterLight(half3 albedo,float3 positionWS,float4 positionCS)
{
    InputData inputData=(InputData)0;
    inputData.positionWS=positionWS;
    inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(positionWS);
    float3 normal=normalize(cross(ddy(positionWS),ddx(positionWS)));
    normal=dot(normal,inputData.viewDirectionWS)<0?-normal:normal;
    inputData.normalWS=normal;
    inputData.shadowCoord=TransformWorldToShadowCoord(positionWS);
    inputData.bakedGI=SampleSH(normal);
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;
    surface.albedo=albedo;surface.alpha=1;surface.occlusion=1;
    return UniversalFragmentBlinnPhong(inputData,surface).rgb;
}

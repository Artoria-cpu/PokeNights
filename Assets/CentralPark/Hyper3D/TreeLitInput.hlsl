// Preserve URP lighting while gently reducing the tree albedo saturation.
#define InitializeStandardLitSurfaceData ParkOriginalLitSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    ParkOriginalLitSurfaceData(uv, surfaceData);
    half luminance = dot(surfaceData.albedo, half3(0.2126, 0.7152, 0.0722));
    surfaceData.albedo = lerp(luminance.xxx, surfaceData.albedo, 0.88h);
}

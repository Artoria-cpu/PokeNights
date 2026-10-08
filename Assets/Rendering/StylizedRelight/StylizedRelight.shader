Shader "Hidden/Stylized/SceneRelight"
{
    // Screen-space relighting for flat / unlit artwork.
    //
    // The scene is drawn exactly as before; this pass reads the opaque colour and the
    // camera depth buffer, rebuilds a world position and a normal for every pixel, and
    // then re-shades the picture. Nothing in the scene, in the materials or in the
    // existing pixel filter has to change.
    //
    // Composition rule: a fully lit surface comes out AS THE ORIGINAL COLOUR. Light is
    // only ever removed (shadow tint) or added as a thin rim / specular highlight, so the
    // artwork can never wash out or look translucent.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Stylized Scene Relight"
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_RelightDepthTexture);

            // ---- Lighting rig -------------------------------------------------------
            float4 _KeyColour;      // rgb tint of the lit side, a = gain (1 keeps the original brightness)
            float4 _ShadowColour;   // rgb tint of the shaded side, a = how bright shadows stay
            float4 _BounceColour;   // rgb, a intensity - light bounced up off the ground, only in shadow
            float4 _RimColour;      // rgb, a intensity
            float3 _KeyDirection;   // world direction pointing TOWARDS the key light
            float3 _RimDirection;   // world direction pointing TOWARDS the rim light

            // ---- Shaping ------------------------------------------------------------
            float _Wrap;
            float _Terminator;
            float _Softness;
            float _Bands;           // 0 or 1 = smooth, >=2 = quantised light bands
            float _ShapeStrength;   // 0 = ignore reconstructed normals entirely
            float _NormalRadius;    // depth tap spacing, in pixels

            // ---- Rim (silhouette detected from depth, NOT from fresnel) --------------
            float _RimRadius;       // thickness in pixels
            float _RimPower;        // directional falloff
            float _RimStrength;

            // ---- Specular -----------------------------------------------------------
            float _SpecSmoothness;
            float _SpecStrength;

            // ---- Contact shadows ----------------------------------------------------
            float _ContactLength;
            float _ContactStrength;
            float _ContactBias;
            float _ContactThickness;
            float _ContactJitter;
            float _ContactSteps;

            // ---- Composition --------------------------------------------------------
            float _Strength;
            float _Debug;           // 0 off, 1 normals, 2 light only, 3 contact shadow, 4 rim mask
            float4 _RelightTexel;   // xy = 1/width, 1/height   zw = width, height

            bool IsBackground(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 1e-7;
                #else
                    return rawDepth >= 1.0 - 1e-7;
                #endif
            }

            int2 ClampPixel(int2 pixel)
            {
                return clamp(pixel, int2(0, 0), int2(_RelightTexel.zw) - 1);
            }

            float2 PixelToUV(int2 pixel)
            {
                return (float2(pixel) + 0.5) * _RelightTexel.xy;
            }

            // Integer texel loads: no UV clamping or dynamic-scaling maths in the way, so
            // the four taps really are the neighbouring pixels.
            float RawAt(int2 pixel)
            {
                return LOAD_TEXTURE2D_X(_RelightDepthTexture, ClampPixel(pixel)).r;
            }

            float3 WorldAt(int2 pixel, out float eyeDepth)
            {
                int2 p = ClampPixel(pixel);
                float raw = RawAt(p);
                eyeDepth = LinearEyeDepth(raw, _ZBufferParams);
                return ComputeWorldSpacePosition(PixelToUV(p), raw, UNITY_MATRIX_I_VP);
            }

            // Marches the depth buffer towards the key light. This is what puts a real
            // cast shadow under the characters and lets limbs shadow the body.
            float ContactShadow(float3 positionWS, float3 normalWS, float3 lightDir, float2 uv)
            {
                if (_ContactStrength <= 0.001 || _ContactSteps < 2.0)
                    return 1.0;

                int steps = (int)clamp(_ContactSteps, 2.0, 64.0);
                float stepLength = max(0.001, _ContactLength) / steps;

                float2 pixel = uv * _RelightTexel.zw;
                float noise = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                float jitter = lerp(0.5, noise, saturate(_ContactJitter));

                float3 p = positionWS + normalWS * _ContactBias + lightDir * stepLength * jitter;
                float occlusion = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    p += lightDir * stepLength;

                    float3 ndc = ComputeNormalizedDeviceCoordinatesWithZ(p, UNITY_MATRIX_VP);
                    if (any(ndc.xy < 0.0) || any(ndc.xy > 1.0))
                        break;

                    float sceneRaw = SAMPLE_TEXTURE2D_X_LOD(_RelightDepthTexture, sampler_PointClamp, ndc.xy, 0).r;
                    if (IsBackground(sceneRaw)) continue;
                    float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                    float rayEye   = LinearEyeDepth(ndc.z, _ZBufferParams);
                    float delta    = rayEye - sceneEye;

                    if (delta > _ContactBias && delta < _ContactThickness)
                    {
                        occlusion = 1.0;
                        break;
                    }
                }

                return 1.0 - occlusion * saturate(_ContactStrength);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);

                int2 centrePixel = int2(uv * _RelightTexel.zw);
                float rawCentre = RawAt(centrePixel);
                if (IsBackground(rawCentre))
                    return source;

                float eyeCentre = LinearEyeDepth(rawCentre, _ZBufferParams);
                float3 positionWS = ComputeWorldSpacePosition(PixelToUV(ClampPixel(centrePixel)), rawCentre, UNITY_MATRIX_I_VP);

                // ---- normal from the nearer neighbour on each axis --------------------
                int r = (int)max(1.0, _NormalRadius);
                float eL, eR, eD, eU;
                float3 pL = WorldAt(centrePixel + int2(-r, 0), eL);
                float3 pR = WorldAt(centrePixel + int2( r, 0), eR);
                float3 pD = WorldAt(centrePixel + int2(0, -r), eD);
                float3 pU = WorldAt(centrePixel + int2(0,  r), eU);

                float3 dx = (abs(eR - eyeCentre) < abs(eyeCentre - eL)) ? (pR - positionWS) : (positionWS - pL);
                float3 dy = (abs(eU - eyeCentre) < abs(eyeCentre - eD)) ? (pU - positionWS) : (positionWS - pD);

                float3 viewVector = _WorldSpaceCameraPos - positionWS;
                float3 N = cross(dy, dx);
                float nLength = length(N);
                N = (nLength > 1e-9) ? N / nLength : normalize(viewVector);
                if (dot(N, viewVector) < 0.0)
                    N = -N;

                float3 V = normalize(viewVector);
                float3 L = normalize(_KeyDirection);

                // ---- diffuse terminator ----------------------------------------------
                float ndl = dot(N, L);
                float wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float shaped = smoothstep(_Terminator - _Softness, _Terminator + _Softness, wrapped);

                if (_Bands >= 2.0)
                {
                    float levels = floor(_Bands) - 1.0;
                    shaped = saturate(floor(shaped * levels + 0.5) / levels);
                }

                shaped = lerp(1.0, shaped, saturate(_ShapeStrength));

                float shadow = ContactShadow(positionWS, N, L, uv);
                float lit = shaped * shadow;

                // ---- rim: a real silhouette, found from a depth jump ------------------
                // Fresnel would smear a haze over the whole body because depth-derived
                // normals are unreliable; a depth discontinuity is exact.
                int rr = (int)max(1.0, _RimRadius);
                float rimL, rimR, rimD, rimU;
                WorldAt(centrePixel + int2(-rr, 0), rimL);
                WorldAt(centrePixel + int2( rr, 0), rimR);
                WorldAt(centrePixel + int2(0, -rr), rimD);
                WorldAt(centrePixel + int2(0,  rr), rimU);

                float farthest = max(max(rimL, rimR), max(rimD, rimU));
                float relativeJump = (farthest - eyeCentre) / max(0.001, eyeCentre);
                float edge = saturate((relativeJump - 0.004) * 150.0);

                // Which way is "outwards" on screen, and does the rim light come from there?
                float2 gradient = float2(rimR - rimL, rimU - rimD);
                float3 ndcHere = ComputeNormalizedDeviceCoordinatesWithZ(positionWS, UNITY_MATRIX_VP);
                float3 ndcLight = ComputeNormalizedDeviceCoordinatesWithZ(positionWS + normalize(_RimDirection) * 0.5, UNITY_MATRIX_VP);
                float2 towardsLight = ndcLight.xy - ndcHere.xy;

                float directional = 0.0;
                if (dot(gradient, gradient) > 1e-12 && dot(towardsLight, towardsLight) > 1e-12)
                    directional = saturate(dot(normalize(gradient), normalize(towardsLight)));

                float rimMask = edge * pow(directional, max(0.25, _RimPower));
                float3 rim = _RimColour.rgb * _RimColour.a * rimMask * _RimStrength;

                // ---- specular ---------------------------------------------------------
                float3 H = normalize(L + V);
                float power = exp2(saturate(_SpecSmoothness) * 11.0) + 1.0;
                float3 specular = _KeyColour.rgb * pow(saturate(dot(N, H)), power) * lit * _SpecStrength;

                // ---- compose: lit == original, shadow only darkens --------------------
                float3 albedo = source.rgb;
                #ifdef UNITY_COLORSPACE_GAMMA
                    albedo = SRGBToLinear(albedo);
                #endif

                float3 litTint = _KeyColour.rgb * _KeyColour.a;
                float3 shadowTint = _ShadowColour.rgb * _ShadowColour.a;
                float3 tint = lerp(shadowTint, litTint, lit);

                // a little warmth bounced back up into the shaded undersides
                float downFacing = saturate(-N.y * 0.5 + 0.5);
                tint += _BounceColour.rgb * _BounceColour.a * downFacing * (1.0 - lit);

                // Edge light follows the painted colour instead of laying a white veil over it.
                float3 shaded = albedo * (tint + rim) + specular;
                shaded = max(0.0, shaded);
                shaded = lerp(albedo, shaded, saturate(_Strength));

                if (_Debug > 0.5)
                {
                    if (_Debug < 1.5)      shaded = N * 0.5 + 0.5;
                    else if (_Debug < 2.5) shaded = tint + rim + specular;
                    else if (_Debug < 3.5) shaded = float3(shadow, shadow, shadow);
                    else                   shaded = float3(edge, rimMask, directional);
                }

                #ifdef UNITY_COLORSPACE_GAMMA
                    shaded = LinearToSRGB(shaded);
                #endif

                return float4(shaded, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

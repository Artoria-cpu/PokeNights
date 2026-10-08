using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Serialization;

/// <summary>
/// Screen-space relighting for a scene that is authored with flat / unlit materials.
///
/// The pass runs after the opaque queue and before transparents, so particles, trails and
/// outlines keep their original emissive look, and the existing pixel filter still runs
/// last on an already lit image. This shapes the authored colours from camera depth;
/// the scene's directional light and character caster passes supply stable ground shadows.
/// </summary>
public sealed class StylizedRelightFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Blend against the untouched image. 1 = fully relit, 0 = off.")]
        [Range(0f, 1f)] public float strength = .7f;

        [Header("Key light")]
        [Tooltip("Elevation of the sun. Higher values push shadows towards the feet.")]
        [Range(-89f, 89f)] public float keyPitch = 46f;
        [Range(-180f, 180f)] public float keyYaw = 38f;
        [ColorUsage(false)] public Color keyColour = new Color(1f, 0.95f, 0.86f);
        [FormerlySerializedAs("keyIntensity"), Range(0f, 3f)] public float keyGain = 1.02f;
        [Tooltip("Use the scene's Sun Source (Lighting window) instead of the angles above.")]
        public bool followSceneSun;

        [Header("Ambient")]
        [Tooltip("Tint of the shaded side; applied as a multiplier to preserve painted colours.")]
        [FormerlySerializedAs("skyColour"), ColorUsage(false)] public Color shadowColour = new Color(1f, .8f, .9f);
        [FormerlySerializedAs("skyIntensity"), Range(0f, 2f)] public float shadowLevel = .74f;
        [Tooltip("Light bounced back up off the ground.")]
        [ColorUsage(false)] public Color bounceColour = new Color(0.55f, 0.45f, 0.38f);
        [Range(0f, 2f)] public float bounceIntensity = .04f;

        [Header("Shaping")]
        [Tooltip("0 = hard lambert falloff, 1 = light wraps all the way around.")]
        [Range(0f, 1f)] public float wrap = 0.22f;
        [Tooltip("Where the lit / shaded split sits.")]
        [Range(0f, 1f)] public float terminator = 0.5f;
        [Tooltip("Width of the split. Small values give the hard, high contrast look.")]
        [Range(0.001f, 0.5f)] public float softness = 0.075f;
        [Tooltip("0 = smooth gradient. 2 or more quantises the light into that many steps.")]
        [Range(0, 8)] public int bands = 2;
        [Range(0f, 1f)] public float shapeStrength = .65f;
        [Tooltip("How many pixels apart the depth taps are when rebuilding normals. Larger = smoother, fewer facets.")]
        [Range(1f, 4f)] public float normalRadius = 1.5f;

        [Header("Rim light")]
        [ColorUsage(false)] public Color rimColour = new Color(0.72f, 0.86f, 1f);
        [Range(0f, 3f)] public float rimIntensity = 1f;
        [Range(0f, 1f)] public float rimStrength = .08f;
        [Range(1f, 4f)] public float rimRadius = 1f;
        [Range(0.25f, 8f)] public float rimPower = 3.5f;
        [Range(-89f, 89f)] public float rimPitch = 12f;
        [Range(-180f, 180f)] public float rimYaw = -150f;

        [Header("Specular")]
        [Range(0f, 1f)] public float specularStrength = 0f;
        [Range(0f, 1f)] public float specularSmoothness = 0.55f;

        [Header("Contact shadows")]
        [Tooltip("Optional screen-space contact supplement. Leave off when using real scene shadows.")]
        [Range(0f, 1f)] public float contactStrength = 0f;
        [Tooltip("How far the shadow can reach, in world units.")]
        [Range(0.1f, 12f)] public float contactLength = 1.2f;
        [Range(4, 64)] public int contactSteps = 16;
        [Tooltip("Push the ray off the surface so a face does not shadow itself.")]
        [Range(0.001f, 0.25f)] public float contactBias = 0.04f;
        [Tooltip("Assumed depth of an occluder. Too large and far geometry casts shadows it should not.")]
        [Range(0.05f, 4f)] public float contactThickness = .12f;
        [Range(0f, 1f)] public float contactJitter = 0f;

        [Header("Debug")]
        public DebugView debugView = DebugView.Off;
    }

    public enum DebugView { Off = 0, Normals = 1, LightingOnly = 2, ContactShadow = 3, RimMask = 4 }

    [Tooltip("Material using Hidden/Stylized/SceneRelight.")]
    public Material material;

    [Tooltip("Where in the frame the relight happens. Before transparents keeps VFX unlit.")]
    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

    public Settings settings = new Settings();

    RelightPass m_Pass;

    public override void Create()
    {
        m_Pass = new RelightPass { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || material.shader == null || settings.strength <= 0f)
            return;

        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        m_Pass.renderPassEvent = injectionPoint;
        m_Pass.material = material;
        m_Pass.settings = settings;

        // Guarantees _CameraDepthTexture exists at this injection point.
        m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(m_Pass);
    }

    static Vector3 TowardsLight(float pitch, float yaw)
    {
        // Matches a Unity directional light: the light travels along forward,
        // so the direction towards it is back.
        return (Quaternion.Euler(pitch, yaw, 0f) * Vector3.back).normalized;
    }

    sealed class RelightPass : ScriptableRenderPass
    {
        static readonly int s_KeyColour = Shader.PropertyToID("_KeyColour");
        static readonly int s_ShadowColour = Shader.PropertyToID("_ShadowColour");
        static readonly int s_ShapeStrength = Shader.PropertyToID("_ShapeStrength");
        static readonly int s_RimRadius = Shader.PropertyToID("_RimRadius");
        static readonly int s_Depth = Shader.PropertyToID("_RelightDepthTexture");
        static readonly int s_Source = Shader.PropertyToID("_BlitTexture");
        static readonly int s_ScaleBias = Shader.PropertyToID("_BlitScaleBias");
        static readonly int s_BounceColour = Shader.PropertyToID("_BounceColour");
        static readonly int s_RimColour = Shader.PropertyToID("_RimColour");
        static readonly int s_KeyDirection = Shader.PropertyToID("_KeyDirection");
        static readonly int s_RimDirection = Shader.PropertyToID("_RimDirection");
        static readonly int s_Wrap = Shader.PropertyToID("_Wrap");
        static readonly int s_Terminator = Shader.PropertyToID("_Terminator");
        static readonly int s_Softness = Shader.PropertyToID("_Softness");
        static readonly int s_Bands = Shader.PropertyToID("_Bands");
        static readonly int s_RimPower = Shader.PropertyToID("_RimPower");
        static readonly int s_RimStrength = Shader.PropertyToID("_RimStrength");
        static readonly int s_SpecSmoothness = Shader.PropertyToID("_SpecSmoothness");
        static readonly int s_SpecStrength = Shader.PropertyToID("_SpecStrength");
        static readonly int s_ContactLength = Shader.PropertyToID("_ContactLength");
        static readonly int s_ContactStrength = Shader.PropertyToID("_ContactStrength");
        static readonly int s_ContactBias = Shader.PropertyToID("_ContactBias");
        static readonly int s_ContactThickness = Shader.PropertyToID("_ContactThickness");
        static readonly int s_ContactJitter = Shader.PropertyToID("_ContactJitter");
        static readonly int s_ContactSteps = Shader.PropertyToID("_ContactSteps");
        static readonly int s_NormalRadius = Shader.PropertyToID("_NormalRadius");
        static readonly int s_Strength = Shader.PropertyToID("_Strength");
        static readonly int s_Debug = Shader.PropertyToID("_Debug");
        static readonly int s_Texel = Shader.PropertyToID("_RelightTexel");

        public Material material;
        public Settings settings;

        public RelightPass()
        {
            // The relight reads and writes full screen colour, so an intermediate target is required.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("Stylized Relight");
        }

        sealed class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle depth;
            public MaterialPropertyBlock properties;
        }

        MaterialPropertyBlock PushSettings(int width, int height)
        {
            var material = new MaterialPropertyBlock();
            Vector3 key = TowardsLight(settings.keyPitch, settings.keyYaw);
            if (settings.followSceneSun && RenderSettings.sun != null)
                key = -RenderSettings.sun.transform.forward;

            material.SetVector(s_KeyColour, new Vector4(settings.keyColour.r, settings.keyColour.g, settings.keyColour.b, settings.keyGain));
            material.SetVector(s_ShadowColour, new Vector4(settings.shadowColour.r, settings.shadowColour.g, settings.shadowColour.b, settings.shadowLevel));
            material.SetVector(s_BounceColour, new Vector4(settings.bounceColour.r, settings.bounceColour.g, settings.bounceColour.b, settings.bounceIntensity));
            material.SetVector(s_RimColour, new Vector4(settings.rimColour.r, settings.rimColour.g, settings.rimColour.b, settings.rimIntensity));
            material.SetVector(s_KeyDirection, key);
            material.SetVector(s_RimDirection, TowardsLight(settings.rimPitch, settings.rimYaw));

            material.SetFloat(s_Wrap, settings.wrap);
            material.SetFloat(s_Terminator, settings.terminator);
            material.SetFloat(s_Softness, settings.softness);
            material.SetFloat(s_Bands, settings.bands);
            material.SetFloat(s_ShapeStrength, settings.shapeStrength);
            material.SetFloat(s_RimRadius, settings.rimRadius);
            material.SetFloat(s_RimPower, settings.rimPower);
            material.SetFloat(s_RimStrength, settings.rimStrength);
            material.SetFloat(s_SpecSmoothness, settings.specularSmoothness);
            material.SetFloat(s_SpecStrength, settings.specularStrength);

            material.SetFloat(s_ContactLength, settings.contactLength);
            material.SetFloat(s_ContactStrength, settings.contactStrength);
            material.SetFloat(s_ContactBias, settings.contactBias);
            material.SetFloat(s_ContactThickness, settings.contactThickness);
            material.SetFloat(s_ContactJitter, settings.contactJitter);
            material.SetFloat(s_ContactSteps, settings.contactSteps);

            material.SetFloat(s_NormalRadius, settings.normalRadius);
            material.SetFloat(s_Strength, settings.strength);
            material.SetFloat(s_Debug, (float)settings.debugView);
            material.SetVector(s_Texel, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            material.SetVector(s_ScaleBias, new Vector4(1, 1, 0, 0));
            return material;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var resources = frame.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resources.activeColorTexture;
            TextureHandle depth = resources.cameraDepthTexture;
            if (!source.IsValid() || !depth.IsValid())
                return;

            var desc = graph.GetTextureDesc(source);
            desc.name = "Stylized Relight";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.useMipMap = false;

            var properties = PushSettings(desc.width, desc.height);

            TextureHandle target = graph.CreateTexture(desc);

            using (var builder = graph.AddRasterRenderPass<PassData>("Stylized Relight", out var data))
            {
                data.material = material;
                data.source = source;
                data.depth = depth;
                data.properties = properties;

                builder.UseTexture(source);
                builder.UseTexture(depth);
                builder.SetRenderAttachment(target, 0);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    d.properties.SetTexture(s_Source, d.source);
                    d.properties.SetTexture(s_Depth, d.depth);
                    context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.properties);
                });
            }

            // This runs before transparents: retain URP's existing camera attachment for its queued draws.
            graph.AddBlitPass(target, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                passName: "Stylized Relight: composite");
        }
    }
}

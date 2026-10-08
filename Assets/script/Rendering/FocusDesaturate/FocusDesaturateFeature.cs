using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// The one thing that stays in colour. Gameplay code fills this in; the renderer feature
/// reads it. Kept as a plain static so nothing has to be wired in the inspector.
/// </summary>
public static class FocusSubject
{
    public struct Part
    {
        public Renderer renderer;
        public int subMeshes;
    }

    /// <summary>0 = everything keeps its colour, 1 = full effect.</summary>
    public static float Weight;

    /// <summary>The renderers that stay in colour.</summary>
    public static Part[] Parts;

    /// <summary>World point the colour radiates out from - usually the subject's chest.</summary>
    public static Vector3 CentreWorld;
    public static bool HasCentre;

    /// <summary>Rebuilds the part list from a character's hierarchy. Skips VFX renderers.</summary>
    public static void Capture(GameObject root)
    {
        if (root == null) { Parts = null; return; }

        var found = root.GetComponentsInChildren<Renderer>(true);
        var parts = new List<Part>(found.Length);
        foreach (var renderer in found)
        {
            if (renderer == null) continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

            int subMeshes = 1;
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                subMeshes = skinned.sharedMesh.subMeshCount;
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) subMeshes = filter.sharedMesh.subMeshCount;
            }

            parts.Add(new Part { renderer = renderer, subMeshes = Mathf.Max(1, subMeshes) });
        }
        Parts = parts.ToArray();
    }

    public static void Clear() { Weight = 0f; Parts = null; HasCentre = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad() { Clear(); }
}

/// <summary>
/// Drains the colour out of everything except <see cref="FocusSubject"/>.
///
/// Runs before post processing, so it sits after the opaque pass, the transparents and the
/// relight, and before the existing pixel filter - which therefore quantises an image that
/// is already grey. No material, shader or scene object needs to change: the subject is
/// stamped into a mask by re-drawing its renderers with an override material, depth tested
/// against the scene so only its visible pixels are marked.
/// </summary>
public sealed class FocusDesaturateFeature : ScriptableRendererFeature
{
    [Tooltip("Material using Hidden/Stylized/FocusDesaturate.")]
    public Material material;

    [Tooltip("Before transparents: particles, trails and slash ribbons are drawn afterwards and keep their colour.")]
    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

    [Header("Look")]
    [Tooltip("Colour left in the world at full effect. 0 is fully grey.")]
    [Range(0f, 1f)] public float saturation = 0f;
    [Tooltip("Tint of the grey. Slightly cool reads as colder, slightly warm as filmic.")]
    [ColorUsage(false)] public Color tint = new Color(0.96f, 0.97f, 1f);
    [Tooltip("Brightness of the drained world. Below 1 pushes it away from the subject.")]
    [Range(0.2f, 1.5f)] public float brightness = 0.92f;

    [Header("Edge")]
    [Tooltip("Width of the soft edge around the subject, in pixels. 0 gives a hard cut-out.")]
    [Range(0f, 12f)] public float edgeRadius = 3f;
    [Tooltip("Lower values push the coloured halo further out from the silhouette.")]
    [Range(0f, 0.9f)] public float edgeBias = 0.18f;

    [Header("Falloff around the subject")]
    [Tooltip("How much the colour survives near the subject. 0 desaturates the world evenly.")]
    [Range(0f, 1f)] public float falloffAmount = 0.5f;
    [Tooltip("Screen distance where the colour starts to drain, as a fraction of screen height.")]
    [Range(0f, 1f)] public float falloffStart = 0.12f;
    [Tooltip("Screen distance where the world is fully drained.")]
    [Range(0.05f, 1.5f)] public float falloffEnd = 0.6f;

    [Header("Mask")]
    [Tooltip("LessEqual is the safe default. Equal also rejects pixels the subject's own alpha clipping threw away, at the cost of needing matching depth.")]
    public CompareFunction maskDepthTest = CompareFunction.LessEqual;

    FocusPass m_Pass;
    Material m_Runtime;

    static readonly int s_ZTest = Shader.PropertyToID("_FocusZTest");
    static readonly int s_Weight = Shader.PropertyToID("_FocusWeight");
    static readonly int s_Saturation = Shader.PropertyToID("_FocusSaturation");
    static readonly int s_Tint = Shader.PropertyToID("_FocusTint");
    static readonly int s_Edge = Shader.PropertyToID("_FocusEdge");
    static readonly int s_EdgeBias = Shader.PropertyToID("_FocusEdgeBias");
    static readonly int s_Centre = Shader.PropertyToID("_FocusCentre");
    static readonly int s_Falloff = Shader.PropertyToID("_FocusFalloff");

    public override void Create()
    {
        m_Pass = new FocusPass { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || material.shader == null) return;
        if (FocusSubject.Weight <= 0.001f || FocusSubject.Parts == null || FocusSubject.Parts.Length == 0) return;

        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;

        if (m_Runtime == null || m_Runtime.shader != material.shader)
        {
            CoreUtils.Destroy(m_Runtime);
            m_Runtime = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
        }

        var camera = renderingData.cameraData.camera;

        // Where the colour radiates from, in viewport space. z flags whether it is usable.
        var centre = new Vector4(0.5f, 0.5f, 0f, 0f);
        if (camera != null && FocusSubject.HasCentre)
        {
            var viewport = camera.WorldToViewportPoint(FocusSubject.CentreWorld);
            if (viewport.z > 0f) centre = new Vector4(viewport.x, viewport.y, 1f, 0f);
        }
        float aspect = camera != null && camera.pixelHeight > 0
            ? (float)camera.pixelWidth / camera.pixelHeight
            : 1f;

        m_Runtime.SetFloat(s_ZTest, (float)(int)maskDepthTest);
        m_Runtime.SetFloat(s_Weight, Mathf.Clamp01(FocusSubject.Weight));
        m_Runtime.SetFloat(s_Saturation, saturation);
        m_Runtime.SetVector(s_Tint, new Vector4(tint.r, tint.g, tint.b, brightness));
        m_Runtime.SetFloat(s_Edge, edgeRadius);
        m_Runtime.SetFloat(s_EdgeBias, edgeBias);
        m_Runtime.SetVector(s_Centre, centre);
        m_Runtime.SetVector(s_Falloff, new Vector4(falloffStart, Mathf.Max(falloffStart + 0.001f, falloffEnd), falloffAmount, aspect));

        m_Pass.renderPassEvent = injectionPoint;
        m_Pass.material = m_Runtime;
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Runtime);
        m_Runtime = null;
    }

    sealed class FocusPass : ScriptableRenderPass
    {
        public Material material;

        public FocusPass()
        {
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("Focus Desaturate");
        }

        sealed class MaskData
        {
            public Material material;
            public FocusSubject.Part[] parts;
        }

        sealed class CompositeData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle mask;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var parts = FocusSubject.Parts;
            if (parts == null || parts.Length == 0) return;

            var resources = frame.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;

            TextureHandle source = resources.activeColorTexture;
            TextureHandle depth = resources.activeDepthTexture;
            if (!source.IsValid()) return;

            var colourDesc = graph.GetTextureDesc(source);

            var maskDesc = new TextureDesc(colourDesc.width, colourDesc.height)
            {
                name = "Focus Subject Mask",
                format = GraphicsFormat.R8_UNorm,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
                depthBufferBits = DepthBits.None,
                useMipMap = false,
                clearBuffer = true,
                clearColor = Color.clear
            };
            TextureHandle mask = graph.CreateTexture(maskDesc);

            using (var builder = graph.AddRasterRenderPass<MaskData>("Focus Subject Mask", out var data))
            {
                data.material = material;
                data.parts = parts;

                builder.SetRenderAttachment(mask, 0);
                // Read-only depth: the subject is stamped only where it actually won the depth test.
                if (depth.IsValid()) builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((MaskData d, RasterGraphContext context) =>
                {
                    for (int i = 0; i < d.parts.Length; i++)
                    {
                        var renderer = d.parts[i].renderer;
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        for (int sub = 0; sub < d.parts[i].subMeshes; sub++)
                            context.cmd.DrawRenderer(renderer, d.material, sub, 0);
                    }
                });
            }

            var targetDesc = colourDesc;
            targetDesc.name = "Focus Desaturate";
            targetDesc.clearBuffer = false;
            targetDesc.depthBufferBits = DepthBits.None;
            targetDesc.msaaSamples = MSAASamples.None;
            targetDesc.useMipMap = false;
            TextureHandle target = graph.CreateTexture(targetDesc);

            using (var builder = graph.AddRasterRenderPass<CompositeData>("Focus Desaturate", out var data))
            {
                data.material = material;
                data.source = source;
                data.mask = mask;

                builder.UseTexture(source);
                builder.UseTexture(mask);
                builder.SetRenderAttachment(target, 0);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((CompositeData d, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_FocusMask", d.mask);
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 1);
                });
            }

            graph.AddBlitPass(target, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                passName: "Focus Desaturate: composite");
        }
    }
}

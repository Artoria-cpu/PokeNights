using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public sealed class PixelFilterFeature : ScriptableRendererFeature
{
    public Material material;
    PixelPass pass;
    public override void Create() { pass = new PixelPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing }; }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        var camera = data.cameraData.camera;
        if(material == null || data.cameraData.cameraType != CameraType.Game ||
           !camera.TryGetComponent<PixelFilter>(out var settings) || !settings.isActiveAndEnabled) return;
        pass.material = material;
        renderer.EnqueuePass(pass);
    }
    sealed class PixelPass : ScriptableRenderPass
    {
        public Material material;
        public PixelPass() { requiresIntermediateTexture = true; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var camera = frame.Get<UniversalCameraData>().camera;
            if(!camera.TryGetComponent<PixelFilter>(out var settings) || !settings.isActiveAndEnabled) return;
            var resources = frame.Get<UniversalResourceData>();
            if(resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var fullDesc = graph.GetTextureDesc(source);
            var lowDesc = fullDesc;
            lowDesc.height = Mathf.Min(fullDesc.height, Mathf.Clamp(settings.referenceHeight,144,720));
            lowDesc.width = Mathf.Max(1, Mathf.RoundToInt((float)fullDesc.width / fullDesc.height * lowDesc.height));
            lowDesc.name = "Redhorn Pixel Grid";
            lowDesc.depthBufferBits = DepthBits.None;
            lowDesc.msaaSamples = MSAASamples.None;
            lowDesc.filterMode = FilterMode.Point;
            lowDesc.useMipMap = false;
            lowDesc.clearBuffer = false;
            var low = graph.CreateTexture(lowDesc);
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_ColourLevels", Mathf.Clamp(settings.colourLevels,8,64));
            properties.SetFloat("_ColourStrength", Mathf.Clamp01(settings.colourStrength));
            var sample = new RenderGraphUtils.BlitMaterialParameters(source,low,material,0) { propertyBlock = properties };
            graph.AddBlitPass(sample,passName:"Redhorn: sample fixed pixel grid");
            fullDesc.name = "Redhorn Crisp Pixel Output";
            fullDesc.depthBufferBits = DepthBits.None;
            fullDesc.msaaSamples = MSAASamples.None;
            fullDesc.filterMode = FilterMode.Point;
            fullDesc.clearBuffer = false;
            var result = graph.CreateTexture(fullDesc);
            graph.AddBlitPass(low,result,Vector2.one,Vector2.zero,filterMode:RenderGraphUtils.BlitFilterMode.ClampNearest,passName:"Redhorn: nearest-neighbour enlargement");
            resources.cameraColor = result;
        }
    }
}

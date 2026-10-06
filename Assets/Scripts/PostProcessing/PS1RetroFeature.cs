using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// A URP ScriptableRendererFeature that applies PS1-style downsampling, color depth reduction,
/// film grain, and VHS glitch tearing.
/// </summary>
public class PS1RetroFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PS1Settings
    {
        [Header("Resolution Scaling")]
        [Tooltip("If true, downsamples the screen to a fixed height (e.g. 240p) to enforce a retro pixel grid.")]
        public bool downsample = true;
        public int targetHeight = 240;

        [Header("Retro Effects")]
        [Tooltip("Number of distinct colors per channel. 32 simulates 5-bit color (PS1 style).")]
        [Range(8, 256)] public float colorDepth = 32f;
        [Tooltip("Intensity of the multiplicative film grain noise.")]
        [Range(0f, 1f)] public float grainIntensity = 0.15f;
        [Tooltip("Intensity of the VHS screen tearing / RGB split glitch effect. Crank up during jump scares!")]
        [Range(0f, 1f)] public float glitchIntensity = 0.0f;
        
        [Header("References")]
        public Material postProcessMaterial;
    }

    public PS1Settings settings = new PS1Settings();
    private PS1RetroPass _pass;

    public override void Create()
    {
        _pass = new PS1RetroPass(settings);
        // We run this BEFORE URP's post-processing so that URP's UI/Overlays render crisply on top.
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.postProcessMaterial == null)
        {
            Debug.LogWarningFormat("Missing Post Process Material on {0}", name);
            return;
        }
        
        // Skip rendering in Scene View to avoid making the editor unusable, unless you specifically want it.
        // We'll apply it in Game view.
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(_pass);
        }
    }

}

public class PS1RetroPass : ScriptableRenderPass
{
    private PS1RetroFeature.PS1Settings _settings;
    private RTHandle _tempTex;

    public PS1RetroPass(PS1RetroFeature.PS1Settings settings)
    {
        _settings = settings;
    }

    private class PassData
    {
        public TextureHandle source;
        public TextureHandle destination;
        public Material material;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        if (_settings.postProcessMaterial == null) return;
        if (resourceData.isActiveTargetBackBuffer) return;

        TextureHandle source = resourceData.activeColorTexture;

        var desc = renderGraph.GetTextureDesc(source);
        desc.name = "_PS1TempTex";
        desc.clearBuffer = false;
        desc.depthBufferBits = 0;
        desc.filterMode = FilterMode.Point;

        if (_settings.downsample)
        {
            float aspect = (float)desc.width / desc.height;
            desc.height = _settings.targetHeight;
            desc.width = Mathf.RoundToInt(_settings.targetHeight * aspect);
        }

        TextureHandle destination = renderGraph.CreateTexture(desc);

        // Update Material
        _settings.postProcessMaterial.SetFloat("_ColorDepth", _settings.colorDepth);
        _settings.postProcessMaterial.SetFloat("_GrainIntensity", _settings.grainIntensity);
        float runtimeGlitch = Shader.GetGlobalFloat("_PS1_GlitchIntensity");
        float totalGlitch = Mathf.Clamp01(_settings.glitchIntensity + runtimeGlitch);
        _settings.postProcessMaterial.SetFloat("_GlitchIntensity", totalGlitch);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PS1 Retro Pass", out var passData))
        {
            passData.source = source;
            passData.destination = destination;
            passData.material = _settings.postProcessMaterial;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, Vector4.one, data.material, 0);
            });
        }

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("PS1 Retro Pass Blit Back", out var passData))
        {
            passData.source = destination;
            passData.destination = source;
            
            builder.UseTexture(destination, AccessFlags.Read);
            builder.SetRenderAttachment(source, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, Vector4.one, 0.0f, false);
            });
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelateRendererFetaure : ScriptableRendererFeature
{
    [System.Serializable]
    public class PixelateSettings
    {
        public float pixelSize = 64; // Controls blockiness
        public Material material;
    }

    public PixelateSettings settings = new PixelateSettings();
    private PixelatePass _pixelatePass;

    public override void Create()
    {
        _pixelatePass = new PixelatePass(settings.material);
        _pixelatePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material != null)
        {
            settings.material.SetFloat("_PixelSize", settings.pixelSize);
            renderer.EnqueuePass(_pixelatePass);
        }
    }

    private class PixelatePass : ScriptableRenderPass
    {
        private Material _material;

        public PixelatePass(Material material)
        {
            _material = material;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("PixelatePass");
            RenderTargetIdentifier source = renderingData.cameraData.renderer.cameraColorTarget;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(Shader.PropertyToID("_TempPixelate"), descriptor);

            cmd.Blit(source, "_TempPixelate");
            cmd.Blit("_TempPixelate", source, _material);

            cmd.ReleaseTemporaryRT(Shader.PropertyToID("_TempPixelate"));

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    }
}

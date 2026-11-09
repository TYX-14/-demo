using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudShadowRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material 云影材质;

    private CloudShadowPass 云影Pass;

    public override void Create()
    {
        if (云影材质 == null)
            云影材质 = Resources.Load<Material>("CloudShadow/CloudShadow");

        云影Pass = new CloudShadowPass(云影材质)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };

        云影Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (云影材质 == null || 云影Pass == null)
            return;

        Camera camera = renderingData.cameraData.camera;
        if (camera == null)
            return;

        // 游戏相机和 Scene 视图相机可使用云影；跳过反射、预览等辅助相机。
        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            return;

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
            return;

        renderer.EnqueuePass(云影Pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (云影材质 == null || 云影Pass == null)
            return;

        Camera camera = renderingData.cameraData.camera;
        if (camera == null)
            return;

        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            return;

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
            return;

        云影Pass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        云影Pass?.Dispose();
        云影Pass = null;
    }

    private sealed class CloudShadowPass : ScriptableRenderPass
    {
        private readonly Material material;
        private RTHandle source;
        private RTHandle temporaryColor;

        public CloudShadowPass(Material passMaterial)
        {
            material = passMaterial;
        }

        public void SetSource(RTHandle cameraColor)
        {
            source = cameraColor;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref temporaryColor,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_CloudShadowTemporaryColor"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null || temporaryColor == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("全屏云影");
            using (new ProfilingScope(cmd, new ProfilingSampler("全屏云影")))
            {
                Blitter.BlitCameraTexture(cmd, source, temporaryColor, material, 0);
                Blitter.BlitCameraTexture(cmd, temporaryColor, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            source = null;
        }

        public void Dispose()
        {
            temporaryColor?.Release();
            temporaryColor = null;
            source = null;
        }
    }
}

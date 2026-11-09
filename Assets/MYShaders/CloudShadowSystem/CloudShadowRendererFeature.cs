using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudShadowRendererFeature : ScriptableRendererFeature
{
    private const string 云影Shader名称 = "Hidden/CloudShadow/Fullscreen";

    private Material 云影材质;
    private CloudShadowPass 云影Pass;

    public override void Create()
    {
        // 不再依赖 Resources 文件夹或额外的 .mat 资源。
        // 材质由 Renderer Feature 根据 Shader 临时创建，并在 Dispose 中释放。
        Shader shader = Shader.Find(云影Shader名称);

        if (shader == null)
        {
            Debug.LogError($"找不到云影 Shader：{云影Shader名称}。云影效果已停用。");
            云影Pass = null;
            return;
        }

        云影材质 = CoreUtils.CreateEngineMaterial(shader);

        if (云影材质 == null || !云影材质.shader.isSupported)
        {
            Debug.LogError($"云影 Shader 无法在当前图形设备上使用：{云影Shader名称}。云影效果已停用。");
            CoreUtils.Destroy(云影材质);
            云影材质 = null;
            云影Pass = null;
            return;
        }

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

        CoreUtils.Destroy(云影材质);
        云影材质 = null;
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

            try
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("全屏云影")))
                {
                    // 先将相机颜色处理到临时纹理，再复制回相机颜色目标。
                    // 避免同一张纹理同时作为采样源和写入目标。
                    Blitter.BlitCameraTexture(cmd, source, temporaryColor, material, 0);
                    Blitter.BlitCameraTexture(cmd, temporaryColor, source);
                }

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
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

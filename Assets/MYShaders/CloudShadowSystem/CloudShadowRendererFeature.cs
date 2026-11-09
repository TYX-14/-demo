using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudShadowRendererFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/CloudShadow/Fullscreen";

    private Material material;
    private CloudShadowPass cloudShadowPass;

    public override void Create()
    {
        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            Debug.LogError($"找不到云影 Shader：{ShaderName}");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);

        if (material == null)
        {
            Debug.LogError("云影材质创建失败。");
            return;
        }

        cloudShadowPass = new CloudShadowPass(material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (cloudShadowPass == null || material == null)
            return;

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
            return;

        Camera camera = renderingData.cameraData.camera;

        if (camera == null)
            return;

        if (camera.cameraType != CameraType.Game &&
            camera.cameraType != CameraType.SceneView)
            return;

        renderer.EnqueuePass(cloudShadowPass);
    }

    public override void SetupRenderPasses(
        ScriptableRenderer renderer,
        in RenderingData renderingData)
    {
        if (cloudShadowPass == null)
            return;

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
            return;

        Camera camera = renderingData.cameraData.camera;

        if (camera == null)
            return;

        if (camera.cameraType != CameraType.Game &&
            camera.cameraType != CameraType.SceneView)
            return;

        cloudShadowPass.SetCameraTarget(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        cloudShadowPass?.Dispose();
        cloudShadowPass = null;

        CoreUtils.Destroy(material);
        material = null;
    }

    private sealed class CloudShadowPass : ScriptableRenderPass
    {
        private readonly Material material;

        private RTHandle cameraColor;
        private RTHandle temporaryColor;

        private static readonly ProfilingSampler Profiling =
            new ProfilingSampler("Cloud Shadow Copy Test");

        public CloudShadowPass(Material material)
        {
            this.material = material;
        }

        public void SetCameraTarget(RTHandle target)
        {
            cameraColor = target;
        }

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor =
                renderingData.cameraData.cameraTargetDescriptor;

            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            RenderingUtils.ReAllocateIfNeeded(
                ref temporaryColor,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_CloudShadowTemporary"
            );
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (material == null ||
                cameraColor == null ||
                temporaryColor == null)
                return;

            CommandBuffer cmd =
                CommandBufferPool.Get("Cloud Shadow Copy Test");

            try
            {
                using (new ProfilingScope(cmd, Profiling))
                {
                    // 相机颜色先写入临时纹理，避免同一纹理同时读写。
                    Blitter.BlitCameraTexture(
                        cmd,
                        cameraColor,
                        temporaryColor,
                        material,
                        0
                    );

                    // 再将临时纹理复制回相机颜色目标。
                    Blitter.BlitCameraTexture(
                        cmd,
                        temporaryColor,
                        cameraColor
                    );
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
            cameraColor = null;
        }

        public void Dispose()
        {
            temporaryColor?.Release();
            temporaryColor = null;
            cameraColor = null;
        }
    }
}

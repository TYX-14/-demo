using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 在 URP 后处理（含 Depth of Field）之后，把标记图层再绘制一次。
/// 用途：战斗中的世界空间交互标记（例如角色头顶的小手）需要始终清晰，
/// 但景深是全屏后处理、按深度整片模糊，没有任何"按物体排除"的开关。
/// 把这些标记放到独立图层、由本 Feature 在景深之后重绘，即可完全不受景深影响。
///
/// 使用方式：
/// 1. 在 URP Renderer 资产（如 URP-HighFidelity-Renderer.asset）的 Inspector 中
///    Add Renderer Feature，选择 Battle Marker Render Feature；
/// 2. Marker Layers 勾选 BattleMarker；
/// 3. 相机不要渲染该图层（BattleVFXManager 会在 Camera.onPreCull 中自动剔除）。
/// </summary>
public sealed class BattleMarkerRenderFeature : ScriptableRendererFeature
{
    [SerializeField, Tooltip("需要保持清晰的标记图层。")]
    private LayerMask markerLayers;

    [SerializeField, Tooltip("标记用的 ShaderTag。Sprites/Default 用 SRPDefaultUnlit；URP 的 Sprite-Lit 用 UniversalForward。")]
    private string shaderTag = "SRPDefaultUnlit";

    [SerializeField, Tooltip("可选：强制用指定材质绘制。留空则使用各渲染器自身材质。")]
    private Material overrideMaterial;

    private MarkerPass _pass;

    public override void Create()
    {
        _pass = new MarkerPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    /// <summary>
    /// Feature 是否已在当前 Renderer 资产中启用。
    /// BattleVFXManager 只在它为 true 时才把标记层从相机剔除，
    /// 避免"没装 Feature 却把图层剔掉"导致标记彻底不可见。
    /// </summary>
    public static bool IsActive { get; private set; }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 只处理游戏相机；Scene 视图仍走常规流程，方便编辑时查看。
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        LayerMask layers = ResolveLayers();
        if (layers.value == 0)
            return;

        IsActive = true;

        _pass.Setup(layers, new ShaderTagId(shaderTag), overrideMaterial);
        renderer.EnqueuePass(_pass);
    }

    /// <summary>Inspector 里忘记勾选图层时，按名称回退到 BattleMarker。</summary>
    private LayerMask ResolveLayers()
    {
        if (markerLayers.value != 0)
            return markerLayers;

        int fallback = LayerMask.NameToLayer("BattleMarker");
        return fallback >= 0 ? 1 << fallback : 0;
    }

    private sealed class MarkerPass : ScriptableRenderPass
    {
        private LayerMask _layers;
        private ShaderTagId _tag;
        private Material _overrideMaterial;
        private FilteringSettings _filtering;

        public void Setup(LayerMask layers, ShaderTagId tag, Material overrideMaterial)
        {
            _layers = layers;
            _tag = tag;
            _overrideMaterial = overrideMaterial;

            // 标记是透明精灵，只在 Transparent 队列里筛选。
            _filtering = new FilteringSettings(RenderQueueRange.transparent, layers);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 直接绘制到相机最终颜色目标：此时景深等后处理已经结束。
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            // 相机本身已剔除标记层，因此这里单独按图层重新剔除一次，
            // 拿到只包含标记对象的 CullingResults。
            if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
                return;

            cullingParameters.cullingMask = (uint)_layers.value;

            CommandBuffer cmd = CommandBufferPool.Get("BattleMarkerAfterPost");
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CullingResults cullingResults = context.Cull(ref cullingParameters);

            SortingSettings sorting = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };

            DrawingSettings drawing = new DrawingSettings(_tag, sorting)
            {
                perObjectData = PerObjectData.None
            };

            if (_overrideMaterial != null)
                drawing.overrideMaterial = _overrideMaterial;

            context.DrawRenderers(cullingResults, ref drawing, ref _filtering);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

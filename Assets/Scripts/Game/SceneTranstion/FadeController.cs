

using System;
using UnityEngine.UI;

public class FadeController : singleton<FadeController>
{
    [Header("Default Style")]
    [SerializeField] private FadeStyle defaultStyle = FadeStyle.PanelFade;

    [Header("Fade Panel")]
    [SerializeField] private Image panelImage; // 控制遮罩显隐的 UI 图片组件
    [Header("Wipe Mask")]
    [SerializeField] private RawImage wipeRowImage;

    [Header("Timing")]
    [SerializeField, Range(0.05f, 3f)] private float panelFadeDuration; // 普通淡入淡出的默认持续时间
    [SerializeField, Range(0.05f, 3f)] private float wipeFadeDuration = 1f;  // 擦除（Wipe）模式的默认持续时间

    private FadeStyle _currentStyle;
    private Coroutine _fadeRoutine;//携程

    // 动态覆盖变量：用于单次指定特殊的淡出/淡入时长，-1f 表示不启用覆盖
    private float _nextFadeOutDurationOverride = -1f;
    private float _nextFadeInDurationOverride = -1f;

    private Material _wipeRuntimeMaterial;

    /// <summary>
    /// 初始化组件状态，确保游戏开始时遮罩层不干扰常规 UI 交互
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _currentStyle = defaultStyle;

        panelImage.enabled = false; // 初始关闭图片组件以优化渲染开销
        SetPanelAlpha(0);          // 默认设为全透明


        wipeRowImage.enabled = false;
        _wipeRuntimeMaterial = Instantiate(wipeRowImage.material);
        wipeRowImage.material = _wipeRuntimeMaterial;

        SetProgressAlpha(0);
    }
    /// <summary>
    /// 外部设置淡入淡出模式
    /// </summary>
    public void SetStyle(FadeStyle style)
    {
        _currentStyle = style;
    }

    /// <summary>
    /// 外部动态设置下一次淡入淡出的自定义持续时间（通常用于特定的剧情演出）
    /// </summary>
    /// <param name="fadeInDuration">下一次淡入的时间（小于等于 0 则维持默认值）</param>
    /// <param name="fadeOutDuration">下一次淡出的时间（小于等于 0 则维持默认值）</param>
    public void SetNextFadeDurations(float fadeInDuration, float fadeOutDuration)
    {
        _nextFadeInDurationOverride = fadeInDuration > 0f ? fadeInDuration : -1f;
        _nextFadeOutDurationOverride = fadeOutDuration > 0f ? fadeOutDuration : -1f;
    }

    /// <summary>
    /// 执行淡出效果（画面逐渐变黑/不透明，常用于切场景前遮挡画面）
    /// </summary>
    /// <param name="onDone">淡出动画完全结束后的回调事件</param>
    public void FadeOut(Action onDone = null)
    {
        // 目标 Alpha 设为 1f（全黑不透明），通过 ResolveDuration 动态获取实际耗时
        StartFade(1f, ResolveDuration(true), onDone);
    }

    /// <summary>
    /// 执行淡入效果（画面逐渐变清晰/透明，常用于新场景加载完毕后的显现）
    /// </summary>
    /// <param name="onDone">淡入动画完全结束后的回调事件</param>
    public void FadeIn(Action onDone = null)
    {
        // 目标 Alpha 设为 0f（全透明），通过 ResolveDuration 动态获取实际耗时
        StartFade(0, ResolveDuration(false), onDone);
    }

    /// <summary>
    /// 启动淡入淡出动画的通用核心方法，负责处理新旧动画协程的平滑交替
    /// </summary>
    /// <param name="target">渐变的目标透明度（0 或 1）</param>
    /// <param name="duration">本次渐变动画应该持续的总秒数</param>
    /// <param name="onDone">动画结束后的回调</param>
    private void StartFade(float target, float duration, Action onDone)
    {
        // 如果当前已有淡入淡出协程在运行，先将其强行停止，防止多重渐变冲突混淆
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }

        // 开启新的渐变协程并记录引用
        _fadeRoutine = StartCoroutine(FadeRoutine(target, duration, onDone));
    }

    /// <summary>
    /// 逐帧插值计算并刷新透明度的主要协同程序
    /// </summary>
    /// <param name="target">目标透明度</param>
    /// <param name="duration">动画总持续时间</param>
    /// <param name="onDone">动画完成后的回调</param>
    private IEnumerator FadeRoutine(float target, float duration, Action onDone)
    {
        // 只有在 PanelFade 模式下才需要开启遮罩图，Wipe 擦除模式可能由其他独立逻辑驱动
        panelImage.enabled = _currentStyle == FadeStyle.PanelFade;
        wipeRowImage.enabled = _currentStyle == FadeStyle.WipeMask;

        float start = ReadCurrentValue(); // 缓存当前的初始 Alpha 值（TODO：兼容 wipe 模式的初始值）
        float elapsed = 0f;

        // 在指定时间内，利用 Lerp 进行线性平滑插值
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(start, target, elapsed / duration);
            ApplyValue(alpha); // 逐帧更新图片透明度
            elapsed += Time.deltaTime;
            yield return null;   // 等待下一帧继续执行
        }

        ApplyValue(target); // 确保动画结束时精准到达目标值，消除可能存在的浮点误差

        if (target < 0.0001f)
        {
            panelImage.enabled = false;
            wipeRowImage.enabled= false;
            SetPanelAlpha(0);
            SetProgressAlpha(0);
        }


        onDone?.Invoke();      // 安全触发结束事件
        _fadeRoutine = null;   // 清空协程引用，标记当前处于非动画状态
    }

    /// <summary>
    /// 读取当前过渡效果的进度值。
    /// 返回值范围通常为 0~1。
    /// </summary>
    private float ReadCurrentValue()
    {
        // 如果当前是纯面板淡入淡出模式
        if (_currentStyle == FadeStyle.PanelFade)
        {
            // 直接读取图片透明度(A通道)
            return panelImage.color.a;
        }

        // 如果是Wipe（擦除）效果
        // 从运行时材质中读取 _Progress 参数
        return _wipeRuntimeMaterial.GetFloat("_Progress");
    }

    /// <summary>
    /// 应用新的过渡进度值。
    /// value01 范围为 0~1。
    /// </summary>
    private void ApplyValue(float value01)
    {
        // 将输入值限制在0~1之间
        value01 = Mathf.Clamp01(value01);

        // 面板淡入淡出模式
        if (_currentStyle == FadeStyle.PanelFade)
        {
            // 修改面板透明度
            SetPanelAlpha(value01);

            // 处理完成直接返回
            return;
        }

        // Wipe模式
        // 更新材质中的进度参数
        SetProgressAlpha(value01);
    }

    /// <summary>
    /// 修改面板图片颜色的 Alpha 通道，实现视觉上的显隐控制
    /// </summary>
    /// <param name="alpha">目标透明度值（0f 到 1f）</param>
    private void SetPanelAlpha(float alpha)
    {
        Color color = panelImage.color;
        color.a = alpha;
        panelImage.color = color;
    }

    private void SetProgressAlpha(float progress)
    {
        _wipeRuntimeMaterial.SetFloat("_Progress", (progress));
    }

    /// <summary>
    /// 解析并计算出当前动作实际应该使用的持续秒数（优先使用临时覆盖值，其次使用模式默认值）
    /// </summary>
    /// <param name="isFadeOut">是否为淡出效果（true 为淡出，false 为淡入）</param>
    /// <returns>计算后最终执行的秒数（float）</returns>
    private float ResolveDuration(bool isFadeOut)
    {
        // 逻辑流 1：如果是淡出（变黑）操作，且外部设置了有效的临时覆盖时间
        if (isFadeOut && _nextFadeOutDurationOverride >= 0f)
        {
            float value = _nextFadeOutDurationOverride;
            _nextFadeOutDurationOverride = -1f; // 阅后即焚：取出该值后重置为 -1f，确保只生效一次
            return value;
        }

        // 逻辑流 2：如果是淡入（变清晰）操作，且外部设置了有效的临时覆盖时间
        if (!isFadeOut && _nextFadeInDurationOverride >= 0f)
        {
            float value = _nextFadeInDurationOverride;
            _nextFadeInDurationOverride = -1f; // 阅后即焚：同理重置
            return value;
        }

        // 逻辑流 3：若均无单次覆盖值，则根据当前风格配置（Fade 还是 Wipe）返回对应的面板默认持续时间
        return _currentStyle == FadeStyle.PanelFade ? panelFadeDuration : wipeFadeDuration;
    }
}

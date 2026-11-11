using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 飘字的视觉类型。它只决定数字精灵的颜色，不参与伤害或治疗计算。
/// </summary>
public enum FloatingTextType
{
    StressDamage,       // 压力增加：白色，Sprite 0-9
    PhysicalDamage,     // 物理伤害：红色，Sprite 10-19
    Healing,            // 治疗：绿色，Sprite 20-29
    MagicalDamage,      // 魔法伤害：蓝色，Sprite 30-39
    StressRecovery      // 压力恢复：金色，Sprite 60-69
}

/// <summary>
/// 挂在飘字预制体根物体上。
/// TMP 文本组件需要提前指定包含对应数字精灵的 TMP Sprite Asset。
/// 预制体原有的 Animator / 粒子 / 位移动画不由本脚本接管。
/// </summary>
public sealed class BattleFloatingText : MonoBehaviour
{
    [Header("文本引用")]
    [SerializeField, Tooltip("飘字使用的 TextMeshPro 组件。留空时自动从子物体查找。")]
    private TMP_Text text;

    [Header("运动表现")]
    [SerializeField, Min(0.01f), Tooltip("飘字从出现到销毁的持续时间，使用非缩放时间。")]
    private float animationDuration = 0.32f;

    [SerializeField, Min(0f), Tooltip("飘字在动画期间移动的总距离。")]
    private float driftDistance = 0.75f;

    [SerializeField, Min(0f), Tooltip("运动方向中向上的分量。数值越大，飘字越偏向上方。")]
    private float upwardDriftRatio = 0.8f;

    [SerializeField, Min(0f), Tooltip("运动方向中沿攻击方向的横向分量。")]
    private float horizontalDriftRatio = 0.4f;

    [SerializeField, Range(0f, 1f), Tooltip("动画进行到该比例时开始渐隐。0 表示立即渐隐，1 表示直到结束前都保持清晰。")]
    private float fadeStartRatio = 0.18f;

    [Header("暴击表现")]
    [SerializeField, Min(1f), Tooltip("暴击飘字的放大倍率。颜色仍然沿用原伤害类型。")]
    private float criticalScaleMultiplier = 1.3f;

    [SerializeField, Tooltip("只缩放数字文本，不影响预制体根物体上的位移动画。留空时默认使用文本自身。")]
    private Transform textScaleRoot;

    private Vector3 _originalTextScale;

    private void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        if (text == null)
        {
            Debug.LogError("BattleFloatingText：没有找到 TMP_Text，请在 Inspector 中指定飘字文本组件。", this);
            enabled = false;
            return;
        }

        if (textScaleRoot == null)
            textScaleRoot = text.transform;

        _originalTextScale = textScaleRoot.localScale;
    }

    /// <summary>
    /// 设置飘字内容。
    /// amount 传入正数或负数均可，显示时只展示其绝对值。
    /// </summary>
    public void Show(int amount, FloatingTextType type, bool isCritical = false)
    {
        if (text == null)
        {
            Debug.LogError("BattleFloatingText：文本组件未正确初始化。", this);
            return;
        }

        if (!TryGetSpriteBaseIndex(type, out int baseIndex))
        {
            Debug.LogError($"BattleFloatingText：未配置的飘字类型 {type}。", this);
            return;
        }

        text.richText = true;
        text.text = BuildSpriteNumber(System.Math.Abs((long)amount), baseIndex);

        if (textScaleRoot != null)
        {
            float scale = isCritical ? Mathf.Max(1f, criticalScaleMultiplier) : 1f;
            textScaleRoot.localScale = _originalTextScale * scale;
        }
    }

    /// <summary>
    /// 播放短暂的飘动与渐隐，并在动画结束后销毁自身。
    /// 使用非缩放时间，确保暂停或慢动作时仍能按攻击特写时序结束。
    /// </summary>
    public void PlayAnimation(Vector3 horizontalDirection)
    {
        if (text == null)
            return;

        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude > 0.0001f)
            horizontalDirection.Normalize();

        Vector3 driftDirection =
            Vector3.up * upwardDriftRatio +
            horizontalDirection * horizontalDriftRatio;

        if (driftDirection.sqrMagnitude < 0.0001f)
            driftDirection = Vector3.up;

        StartCoroutine(AnimateAndDestroy(
            driftDirection.normalized,
            Mathf.Max(0.01f, animationDuration),
            Mathf.Max(0f, driftDistance)));
    }

    private IEnumerator AnimateAndDestroy(
        Vector3 driftDirection,
        float duration,
        float distance)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + driftDirection * distance;
        Color originalColor = text.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(startPosition, endPosition, t);

            float fadeDuration = 1f - fadeStartRatio;
            float fade = fadeDuration <= 0f
                ? (t >= 1f ? 1f : 0f)
                : Mathf.Clamp01((t - fadeStartRatio) / fadeDuration);

            Color currentColor = originalColor;
            currentColor.a = originalColor.a * (1f - fade);
            text.color = currentColor;

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>按 Sprite Asset 中连续排列的 0-9 数字索引拼接 TMP 精灵标签。</summary>
    private static string BuildSpriteNumber(long value, int baseIndex)
    {
        string digits = value.ToString();
        StringBuilder builder = new StringBuilder(digits.Length * 12);

        for (int i = 0; i < digits.Length; i++)
        {
            int digit = digits[i] - '0';
            builder.Append("<sprite=");
            builder.Append(baseIndex + digit);
            builder.Append('>');
        }

        return builder.ToString();
    }

    private static bool TryGetSpriteBaseIndex(FloatingTextType type, out int baseIndex)
    {
        switch (type)
        {
            case FloatingTextType.StressDamage:
                baseIndex = 0;
                return true;

            case FloatingTextType.PhysicalDamage:
                baseIndex = 10;
                return true;

            case FloatingTextType.Healing:
                baseIndex = 20;
                return true;

            case FloatingTextType.MagicalDamage:
                baseIndex = 30;
                return true;

            case FloatingTextType.StressRecovery:
                baseIndex = 60;
                return true;

            default:
                baseIndex = 0;
                return false;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 五圆点压力显示组件。每个圆点代表 20 点压力，五个圆点对应 0-100。
/// 100-200 阶段使用高亮珊瑚红到琥珀橙的渐变，避免与暗色背景融合。
/// Outline 通过周期性改变透明度与扩散距离，产生呼吸灯式脉冲效果。
/// </summary>
public class StressMeterUI : MonoBehaviour
{
    [SerializeField] private Image[] stressCircles = new Image[5];
    [SerializeField] private Outline[] glowOutlines = new Outline[5];

    [Header("颜色")]
    [SerializeField] private Color emptyColor = new Color(0.82f, 0.84f, 0.88f, 1f);
    [SerializeField] private Color stressColor = new Color(1f, 0.29f, 0.25f, 1f);
    [SerializeField] private Color afflictionColor = new Color(1f, 0.67f, 0.20f, 1f);
    [SerializeField] private Color virtueColor = new Color(1f, 0.78f, 0.22f, 1f);

    [Header("呼吸灯效果")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 1.6f;
    [SerializeField, Min(0f)] private float minGlowDistance = 1.5f;
    [SerializeField, Min(0f)] private float maxGlowDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float minGlowAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxGlowAlpha = 1f;

    private CharacterRuntimeData boundCharacter;
    private readonly Vector2[] baseGlowDistances = new Vector2[5];
    private float pulseTime;

    private void Awake()
    {
        for (int i = 0; i < baseGlowDistances.Length; i++)
        {
            if (glowOutlines != null && i < glowOutlines.Length && glowOutlines[i] != null)
                baseGlowDistances[i] = glowOutlines[i].effectDistance;
            else
                baseGlowDistances[i] = Vector2.one * 2f;
        }
    }

    private void OnEnable()
    {
        CharacterStressSystem.CharacterUpdated += HandleCharacterUpdated;
        Refresh();
    }

    private void OnDisable()
    {
        CharacterStressSystem.CharacterUpdated -= HandleCharacterUpdated;
    }

    private void Update()
    {
        if (glowOutlines == null || boundCharacter == null)
            return;

        pulseTime += Time.unscaledDeltaTime * pulseSpeed * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(pulseTime) + 1f) * 0.5f;
        float distance = Mathf.Lerp(minGlowDistance, maxGlowDistance, pulse);
        float alpha = Mathf.Lerp(minGlowAlpha, maxGlowAlpha, pulse);

        int stress = Mathf.Clamp(boundCharacter.CurrentStress, 0, 200);
        bool virtue = boundCharacter.SpiritState == CharacterSpiritState.Virtue;
        float secondStage = Mathf.Clamp01((stress - 100) / 100f);
        Color glowBase = virtue
            ? virtueColor
            : (stress >= 100 ? Color.Lerp(stressColor, afflictionColor, secondStage) : stressColor);

        for (int i = 0; i < Mathf.Min(5, glowOutlines.Length); i++)
        {
            Outline glow = glowOutlines[i];
            if (glow == null || !glow.enabled)
                continue;

            Color pulseColor = glowBase;
            pulseColor.a = alpha;
            glow.effectColor = pulseColor;
            glow.effectDistance = Vector2.one * distance;
        }
    }

    /// <summary>将该压力条绑定到一个角色的运行时数据。</summary>
    public void Bind(CharacterRuntimeData character)
    {
        boundCharacter = character;
        Refresh();
    }

    private void HandleCharacterUpdated(CharacterRuntimeData character)
    {
        if (character == boundCharacter)
            Refresh();
    }

    private void Refresh()
    {
        if (stressCircles == null)
            return;

        if (boundCharacter == null)
        {
            for (int i = 0; i < stressCircles.Length; i++)
            {
                if (stressCircles[i] != null)
                {
                    stressCircles[i].fillAmount = 0f;
                    stressCircles[i].color = emptyColor;
                }

                if (glowOutlines != null && i < glowOutlines.Length && glowOutlines[i] != null)
                    glowOutlines[i].enabled = false;
            }
            return;
        }

        int stress = Mathf.Clamp(boundCharacter.CurrentStress, 0, 200);
        bool virtue = boundCharacter.SpiritState == CharacterSpiritState.Virtue;
        float secondStage = Mathf.Clamp01((stress - 100) / 100f);

        for (int i = 0; i < Mathf.Min(5, stressCircles.Length); i++)
        {
            Image circle = stressCircles[i];
            if (circle == null)
                continue;

            float progress = Mathf.Clamp01((stress - i * 20) / 20f);
            Color targetColor;

            if (virtue)
            {
                targetColor = virtueColor;
                circle.fillAmount = 1f;
            }
            else if (stress >= 100)
            {
                targetColor = Color.Lerp(stressColor, afflictionColor, secondStage);
                circle.fillAmount = 1f;
            }
            else
            {
                targetColor = Color.Lerp(emptyColor, stressColor, progress);
                circle.fillAmount = progress;
            }

            circle.color = targetColor;

            if (glowOutlines != null && i < glowOutlines.Length && glowOutlines[i] != null)
            {
                Outline glow = glowOutlines[i];
                glow.enabled = virtue || stress >= (i + 1) * 20;
                glow.effectColor = targetColor;
                glow.effectDistance = baseGlowDistances[i];
            }
        }
    }
}

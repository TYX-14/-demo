using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 五圆点压力显示组件。每个圆点代表 20 点压力，五个圆点对应 0-100。
/// 100-200 阶段保持五点填满，并逐渐由红色转为暗红色。
/// 请在 Inspector 中绑定 5 个 Image；若要显示逐点填充，请将 Image Type 设为 Filled。
/// </summary>
public class StressMeterUI : MonoBehaviour
{
    [SerializeField] private Image[] stressCircles = new Image[5];
    [SerializeField] private Outline[] glowOutlines = new Outline[5];

    [Header("颜色")]
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color stressColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color afflictionColor = new Color(0.18f, 0.015f, 0.025f, 1f);
    [SerializeField] private Color virtueColor = new Color(1f, 0.72f, 0.12f, 1f);

    private CharacterRuntimeData boundCharacter;

    private void OnEnable()
    {
        CharacterStressSystem.CharacterUpdated += HandleCharacterUpdated;
        Refresh();
    }

    private void OnDisable()
    {
        CharacterStressSystem.CharacterUpdated -= HandleCharacterUpdated;
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
        if (boundCharacter == null || stressCircles == null)
            return;

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
                glow.effectColor = virtue
                    ? virtueColor
                    : (stress >= 100
                        ? Color.Lerp(stressColor, afflictionColor, secondStage)
                        : stressColor);
            }
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 技能列表中的单个显示条目。
/// 负责显示技能信息，并在鼠标悬停时展示技能说明。
/// </summary>
public class BattleSkillEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("可在 Inspector 中分别绑定")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text usablePositionsText;
    [SerializeField] private TMP_Text targetPositionsText;
    [SerializeField] private TMP_Text spCostText;

    private GameObject skillTip;
    private TMP_Text skillTipDescription;

    private void Awake()
    {
        // Tip 已经放在按钮预制体下，按名称自动获取，避免额外改动预制体引用。
        Transform tipTransform = transform.Find("Skill Tip");
        if (tipTransform == null)
            tipTransform = FindChildByName(transform, "Skill Tip");

        if (tipTransform != null)
        {
            skillTip = tipTransform.gameObject;
            skillTipDescription = skillTip.GetComponentInChildren<TMP_Text>(true);
            skillTip.SetActive(false);

            // Tip 只用于展示，不能挡住按钮的指针事件，否则悬停时可能闪烁。
            foreach (Graphic graphic in skillTip.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

    public void Setup(SkillDefinitionSO skill)
    {
        if (skill == null)
        {
            Clear();
            return;
        }

        if (skillIcon != null)
        {
            skillIcon.sprite = skill.SkillIcon;
            skillIcon.enabled = skill.SkillIcon != null;
        }

        if (skillNameText != null)
            skillNameText.text = skill.SkillName;

        if (usablePositionsText != null)
            usablePositionsText.text = $"自身站位：{FormatPositions(skill.UsablePositions)}";

        if (targetPositionsText != null)
            targetPositionsText.text = $"目标站位：{FormatPositions(skill.TargetPositions)}";

        if (spCostText != null)
            spCostText.text = $"{skill.SpCost} SP";

        if (skillTipDescription != null)
            skillTipDescription.text = skill.Description ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (skillTip != null)
            skillTip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (skillTip != null)
            skillTip.SetActive(false);
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private string FormatPositions(IReadOnlyList<int> positions)
    {
        if (positions == null || positions.Count == 0)
            return "无";

        List<string> labels = new();
        for (int i = 0; i < positions.Count; i++)
            labels.Add(positions[i].ToString());

        return $"{string.Join("、", labels)}号位";
    }

    private void Clear()
    {
        if (skillIcon != null)
        {
            skillIcon.sprite = null;
            skillIcon.enabled = false;
        }

        if (skillNameText != null)
            skillNameText.text = string.Empty;

        if (usablePositionsText != null)
            usablePositionsText.text = string.Empty;

        if (targetPositionsText != null)
            targetPositionsText.text = string.Empty;

        if (spCostText != null)
            spCostText.text = string.Empty;

        if (skillTipDescription != null)
            skillTipDescription.text = string.Empty;

        if (skillTip != null)
            skillTip.SetActive(false);
    }
}

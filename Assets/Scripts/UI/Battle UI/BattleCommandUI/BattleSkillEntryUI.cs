using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能列表中的单个显示条目。
/// 只负责把技能数据呈现到 UI，不执行技能效果。
/// </summary>
public class BattleSkillEntryUI : MonoBehaviour
{
    [Header("可在 Inspector 中分别绑定")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text usablePositionsText;
    [SerializeField] private TMP_Text targetPositionsText;
    [SerializeField] private TMP_Text spCostText;

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
    }
}

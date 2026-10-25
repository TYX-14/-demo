using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能按钮上的八点站位示意图。
/// 敌方从左到右为 4、3、2、1 号位；友方从左到右为 1、2、3、4 号位。
/// 单体技能只高亮可选目标；AoE 技能高亮攻击位置，并连接相邻的高亮点。
/// </summary>
public class SkillPositionDiagram : MonoBehaviour
{
    [Header("敌方站位点（从左到右绑定 4、3、2、1 号位）")]
    [SerializeField] private Image[] enemyPositionDots = new Image[4];

    [Header("友方站位点（从左到右绑定 1、2、3、4 号位）")]
    [SerializeField] private Image[] allyPositionDots = new Image[4];

    [Header("敌方 AoE 连接线（从左到右连接 4-3、3-2、2-1 号位）")]
    [SerializeField] private Image[] enemyAreaConnections = new Image[3];

    [Header("显示颜色")]
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color selectedInactiveColor = new Color(0f, 0f, 0f, 0.25f);
    [SerializeField] private Color targetableColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color usableColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color areaColor = new Color(1f, 0.75f, 0.2f, 1f);

    private SkillDefinitionSO currentSkill;
    private bool isSelected;

    private void Awake()
    {
        Refresh();
    }

    public void Setup(SkillDefinitionSO skill)
    {
        currentSkill = skill;
        Refresh();
    }

    /// <summary>
    /// 根据技能按钮的选中状态切换未激活点的颜色。
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        Refresh();
    }

    public void Clear()
    {
        currentSkill = null;
        Refresh();
    }

    private void Refresh()
    {
        Color inactive = isSelected ? selectedInactiveColor : inactiveColor;

        if (currentSkill == null)
        {
            SetAllDots(enemyPositionDots, inactive);
            SetAllDots(allyPositionDots, inactive);
            HideAreaConnections();
            return;
        }

        SetEnemyDots(currentSkill.TargetPositions, targetableColor, inactive);
        SetDots(allyPositionDots, currentSkill.UsablePositions, usableColor, inactive);

        if (currentSkill.TargetType == SkillTargetType.AoE)
        {
            SetEnemyDots(currentSkill.TargetPositions, areaColor, inactive);
            SetAreaConnections(currentSkill.TargetPositions);
        }
        else
        {
            HideAreaConnections();
        }
    }

    private void SetEnemyDots(IReadOnlyList<int> activePositions, Color activeColor, Color inactive)
    {
        if (enemyPositionDots == null)
            return;

        for (int i = 0; i < enemyPositionDots.Length; i++)
        {
            if (enemyPositionDots[i] == null)
                continue;

            int positionNumber = 4 - i;
            enemyPositionDots[i].color =
                ContainsPosition(activePositions, positionNumber) ? activeColor : inactive;
        }
    }

    private void SetDots(Image[] dots, IReadOnlyList<int> activePositions, Color activeColor, Color inactive)
    {
        if (dots == null)
            return;

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
                continue;

            dots[i].color = ContainsPosition(activePositions, i + 1) ? activeColor : inactive;
        }
    }

    private void SetAllDots(Image[] dots, Color color)
    {
        if (dots == null)
            return;

        foreach (Image dot in dots)
        {
            if (dot != null)
                dot.color = color;
        }
    }

    private void SetAreaConnections(IReadOnlyList<int> positions)
    {
        if (enemyAreaConnections == null)
            return;

        for (int i = 0; i < enemyAreaConnections.Length; i++)
        {
            Image connection = enemyAreaConnections[i];
            if (connection == null)
                continue;

            int leftPosition = 4 - i;
            int rightPosition = 3 - i;
            bool show = ContainsPosition(positions, leftPosition) &&
                        ContainsPosition(positions, rightPosition);

            connection.gameObject.SetActive(show);
            if (show)
                connection.color = areaColor;
        }
    }

    private void HideAreaConnections()
    {
        if (enemyAreaConnections == null)
            return;

        foreach (Image connection in enemyAreaConnections)
        {
            if (connection != null)
                connection.gameObject.SetActive(false);
        }
    }

    private bool ContainsPosition(IReadOnlyList<int> positions, int position)
    {
        if (positions == null)
            return false;

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i] == position)
                return true;
        }

        return false;
    }
}

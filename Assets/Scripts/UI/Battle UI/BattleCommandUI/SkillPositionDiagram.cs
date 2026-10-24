using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能按钮上的八点站位示意图。
/// 左侧显示敌方可选目标与固定 AoE 覆盖范围，右侧显示友方使用者的可用站位。
/// 此组件只负责显示，不负责战斗目标选择或伤害计算。
/// </summary>
public class SkillPositionDiagram : MonoBehaviour
{
    [Header("敌方站位点（左侧，按 1~4 号位顺序绑定）")]
    [SerializeField] private Image[] enemyPositionDots = new Image[4];

    [Header("友方站位点（右侧，按 1~4 号位顺序绑定）")]
    [SerializeField] private Image[] allyPositionDots = new Image[4];

    [Header("敌方 AoE 连接线（连接 1-2、2-3、3-4 号位）")]
    [SerializeField] private Image[] enemyAreaConnections = new Image[3];

    [Header("显示颜色")]
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color targetableColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color usableColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color areaColor = new Color(1f, 0.75f, 0.2f, 1f);

    private void Awake()
    {
        Clear();
    }

    /// <summary>
    /// 根据技能配置刷新站位点和固定 AoE 范围。
    /// </summary>
    public void Setup(SkillDefinitionSO skill)
    {
        if (skill == null)
        {
            Clear();
            return;
        }

        SetDots(enemyPositionDots, skill.TargetPositions, targetableColor);
        SetDots(allyPositionDots, skill.UsablePositions, usableColor);

        // AoE 覆盖点使用独立颜色，覆盖普通的可选目标点显示。
        IReadOnlyList<int> areaPositions = skill.FixedAreaPositions;
        if (areaPositions != null && areaPositions.Count > 0)
        {
            for (int i = 0; i < areaPositions.Count; i++)
            {
                int positionIndex = areaPositions[i] - 1;
                if (positionIndex >= 0 && positionIndex < enemyPositionDots.Length &&
                    enemyPositionDots[positionIndex] != null)
                {
                    enemyPositionDots[positionIndex].color = areaColor;
                }
            }
        }

        SetAreaConnections(areaPositions);
    }

    public void Clear()
    {
        SetAllDots(enemyPositionDots, inactiveColor);
        SetAllDots(allyPositionDots, inactiveColor);

        if (enemyAreaConnections == null)
            return;

        foreach (Image connection in enemyAreaConnections)
        {
            if (connection != null)
                connection.gameObject.SetActive(false);
        }
    }

    private void SetDots(Image[] dots, IReadOnlyList<int> activePositions, Color activeColor)
    {
        if (dots == null)
            return;

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
                continue;

            bool active = ContainsPosition(activePositions, i + 1);
            dots[i].color = active ? activeColor : inactiveColor;
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

    private void SetAreaConnections(IReadOnlyList<int> areaPositions)
    {
        if (enemyAreaConnections == null)
            return;

        for (int i = 0; i < enemyAreaConnections.Length; i++)
        {
            Image connection = enemyAreaConnections[i];
            if (connection == null)
                continue;

            // 连接线 i 对应 i+1 与 i+2 号位；只有相邻两点都属于 AoE 时才显示。
            bool show = ContainsPosition(areaPositions, i + 1) &&
                        ContainsPosition(areaPositions, i + 2);
            connection.gameObject.SetActive(show);
            if (show)
                connection.color = areaColor;
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能按钮上的八点站位示意图。
/// 敌方从左到右为 4、3、2、1 号位；友方从左到右为 1、2、3、4 号位。
/// 单体技能只高亮可选目标；AoE 技能高亮攻击位置，并连接相邻的高亮点。
/// 此组件只负责显示，不负责战斗目标选择或伤害计算。
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
    [SerializeField] private Color targetableColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color usableColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color areaColor = new Color(1f, 0.75f, 0.2f, 1f);

    private void Awake()
    {
        Clear();
    }

    public void Setup(SkillDefinitionSO skill)
    {
        if (skill == null)
        {
            Clear();
            return;
        }

        // 敌方点在视觉上反向排列，因此把站位编号映射到反向数组索引。
        SetEnemyDots(skill.TargetPositions, targetableColor);
        SetDots(allyPositionDots, skill.UsablePositions, usableColor);

        bool isAoE = skill.TargetType == SkillTargetType.AoE;
        if (isAoE)
        {
            SetEnemyDots(skill.TargetPositions, areaColor);
            SetAreaConnections(skill.TargetPositions);
        }
        else
        {
            HideAreaConnections();
        }
    }

    public void Clear()
    {
        SetAllDots(enemyPositionDots, inactiveColor);
        SetAllDots(allyPositionDots, inactiveColor);
        HideAreaConnections();
    }

    private void SetEnemyDots(IReadOnlyList<int> activePositions, Color activeColor)
    {
        if (enemyPositionDots == null)
            return;

        for (int i = 0; i < enemyPositionDots.Length; i++)
        {
            if (enemyPositionDots[i] == null)
                continue;

            int positionNumber = 4 - i;
            enemyPositionDots[i].color =
                ContainsPosition(activePositions, positionNumber) ? activeColor : inactiveColor;
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

    private void SetAreaConnections(IReadOnlyList<int> positions)
    {
        if (enemyAreaConnections == null)
            return;

        for (int i = 0; i < enemyAreaConnections.Length; i++)
        {
            Image connection = enemyAreaConnections[i];
            if (connection == null)
                continue;

            // 视觉上从左到右依次连接 4-3、3-2、2-1。
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

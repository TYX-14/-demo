using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能按钮上的八点站位示意图。
/// 目标关系决定目标点显示在哪一侧；此组件只负责显示，不负责目标合法性判断。
/// </summary>
public class SkillPositionDiagram : MonoBehaviour
{
    [Header("敌方站位点（从左到右绑定 4、3、2、1 号位）")]
    [SerializeField] private Image[] enemyPositionDots = new Image[4];

    [Header("友方站位点（从左到右绑定 1、2、3、4 号位）")]
    [SerializeField] private Image[] allyPositionDots = new Image[4];

    [Header("敌方 AoE 连接线（从左到右连接 4-3、3-2、2-1 号位）")]
    [SerializeField] private Image[] enemyAreaConnections = new Image[3];

    [Header("友方 AoE 连接线（从左到右分别绑定 1-2、2-3、3-4 号位）")]
    [Tooltip("与敌方 AoE 连接线相同：仅当技能为 AoE，且连接线两端的站位都在目标位置范围内时显示。")]
    [SerializeField] private Image[] allyAreaConnections = new Image[3];

    [Header("显示颜色")]
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color selectedInactiveColor = new Color(0f, 0f, 0f, 0.25f);
    [SerializeField, Min(0.01f)] private float colorTransitionDuration = 0.15f;
    [Tooltip("敌方单体目标点颜色。敌方 AoE 目标点使用 areaColor。")]
    [SerializeField] private Color targetableColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("可用施放位置颜色。使用紫色，避免按钮选中时的白色背景影响蓝色辨识。")]
    [SerializeField] private Color usableColor = new Color(0.7f, 0.4f, 1f, 1f);
    [Tooltip("敌方 AoE 目标点与敌方 AoE 连线颜色。")]
    [SerializeField] private Color areaColor = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("友方目标点颜色，友方单体与友方 AoE 均使用此颜色。")]
    [SerializeField] private Color allyTargetableColor = new Color(0.25f, 0.65f, 1f, 1f);
    [Tooltip("友方 AoE 连线颜色，默认与友方目标点同为蓝色。")]
    [SerializeField] private Color allyAreaColor = new Color(0.25f, 0.65f, 1f, 1f);

    private readonly Dictionary<Image, Color> baseDotColors = new();
    private readonly HashSet<Image> inactiveDots = new();
    private Coroutine colorTransition;
    private bool isSelected;

    private void Awake()
    {
        Clear();
    }

    public void Setup(SkillDefinitionSO skill)
    {
        StopColorTransition();
        baseDotColors.Clear();
        inactiveDots.Clear();

        if (skill == null)
        {
            ClearDotsImmediately();
            HideAreaConnections();
            return;
        }

        bool selfOnly = skill.TargetRelation == SkillTargetRelation.Self ||
                        skill.TargetSelectionMode == SkillTargetSelectionMode.Self;
        bool hasEntityTarget = skill.TargetSelectionMode != SkillTargetSelectionMode.None;
        bool targetsEnemy = hasEntityTarget && !selfOnly &&
                            (skill.TargetRelation == SkillTargetRelation.Enemy ||
                             skill.TargetRelation == SkillTargetRelation.AllEntities);
        bool targetsAlly = hasEntityTarget &&
                           (skill.TargetRelation == SkillTargetRelation.Ally ||
                            skill.TargetRelation == SkillTargetRelation.AllEntities ||
                            selfOnly);

        if (targetsEnemy)
        {
            Color enemyTargetColor = skill.TargetType == SkillTargetType.AoE
                ? areaColor
                : targetableColor;
            SetEnemyDots(skill.TargetRelation == SkillTargetRelation.AllEntities
                ? new[] { 1, 2, 3, 4 }
                : skill.TargetPositions, enemyTargetColor);
        }
        else
            SetEnemyDots(null, inactiveColor);

        if (targetsAlly && !selfOnly)
            SetDots(allyPositionDots,
                skill.TargetRelation == SkillTargetRelation.AllEntities
                    ? new[] { 1, 2, 3, 4 }
                    : skill.TargetPositions,
                allyTargetableColor);
        else if (selfOnly)
            SetDots(allyPositionDots, new[] { 1, 2, 3, 4 }, allyTargetableColor);
        else if (!selfOnly && hasEntityTarget)
            SetDots(allyPositionDots, skill.UsablePositions, usableColor);
        else
            SetDots(allyPositionDots, null, inactiveColor);

        if (skill.TargetType == SkillTargetType.AoE)
        {
            if (targetsEnemy)
                SetAreaConnections(enemyAreaConnections,
                    skill.TargetRelation == SkillTargetRelation.AllEntities
                        ? new[] { 1, 2, 3, 4 }
                        : skill.TargetPositions, true, areaColor);
            else
                HideConnections(enemyAreaConnections);

            if (targetsAlly)
                SetAreaConnections(allyAreaConnections,
                    skill.TargetRelation == SkillTargetRelation.AllEntities || selfOnly
                        ? new[] { 1, 2, 3, 4 }
                        : skill.TargetPositions, false, allyAreaColor);
            else
                HideConnections(allyAreaConnections);
        }
        else
        {
            HideAreaConnections();
        }

        ApplyDotColorsImmediately(isSelected);
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        StopColorTransition();
        colorTransition = StartCoroutine(TransitionInactiveDots(selected));
    }

    public void Clear()
    {
        StopColorTransition();
        baseDotColors.Clear();
        inactiveDots.Clear();
        RegisterAllDotsAsInactive(enemyPositionDots);
        RegisterAllDotsAsInactive(allyPositionDots);
        ApplyDotColorsImmediately(isSelected);
        HideAreaConnections();
    }

    private void RegisterAllDotsAsInactive(Image[] dots)
    {
        if (dots == null)
            return;

        foreach (Image dot in dots)
        {
            if (dot == null)
                continue;

            baseDotColors[dot] = inactiveColor;
            inactiveDots.Add(dot);
        }
    }

    private void SetEnemyDots(IReadOnlyList<int> activePositions, Color activeColor)
    {
        if (enemyPositionDots == null)
            return;

        for (int i = 0; i < enemyPositionDots.Length; i++)
        {
            Image dot = enemyPositionDots[i];
            if (dot == null)
                continue;

            int positionNumber = 4 - i;
            bool active = ContainsPosition(activePositions, positionNumber);
            RegisterDotColor(dot, active ? activeColor : inactiveColor, !active);
        }
    }

    private void SetDots(Image[] dots, IReadOnlyList<int> activePositions, Color activeColor)
    {
        if (dots == null)
            return;

        for (int i = 0; i < dots.Length; i++)
        {
            Image dot = dots[i];
            if (dot == null)
                continue;

            bool active = ContainsPosition(activePositions, i + 1);
            RegisterDotColor(dot, active ? activeColor : inactiveColor, !active);
        }
    }

    private void RegisterDotColor(Image dot, Color color, bool inactive)
    {
        baseDotColors[dot] = color;
        if (inactive)
            inactiveDots.Add(dot);
        else
            inactiveDots.Remove(dot);
    }

    private void ClearDotsImmediately()
    {
        RegisterAllDotsAsInactive(enemyPositionDots);
        RegisterAllDotsAsInactive(allyPositionDots);
        ApplyDotColorsImmediately(isSelected);
    }

    private void ApplyDotColorsImmediately(bool selected)
    {
        foreach (KeyValuePair<Image, Color> pair in baseDotColors)
        {
            if (pair.Key != null)
                pair.Key.color = selected && inactiveDots.Contains(pair.Key)
                    ? selectedInactiveColor
                    : pair.Value;
        }
    }

    private IEnumerator TransitionInactiveDots(bool selected)
    {
        Dictionary<Image, Color> startColors = new();
        foreach (Image dot in inactiveDots)
        {
            if (dot != null)
                startColors[dot] = dot.color;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, colorTransitionDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Color targetColor = selected ? selectedInactiveColor : inactiveColor;

            foreach (KeyValuePair<Image, Color> pair in startColors)
            {
                if (pair.Key != null)
                    pair.Key.color = Color.Lerp(pair.Value, targetColor, easedProgress);
            }

            yield return null;
        }

        Color finalColor = selected ? selectedInactiveColor : inactiveColor;
        foreach (Image dot in inactiveDots)
        {
            if (dot != null)
                dot.color = finalColor;
        }

        colorTransition = null;
    }

    private void StopColorTransition()
    {
        if (colorTransition == null)
            return;

        StopCoroutine(colorTransition);
        colorTransition = null;
    }

    private void SetAreaConnections(Image[] connections, IReadOnlyList<int> positions, bool reverseOrder, Color connectionColor)
    {
        if (connections == null)
            return;

        for (int i = 0; i < connections.Length; i++)
        {
            Image connection = connections[i];
            if (connection == null)
                continue;

            int firstPosition = reverseOrder ? 4 - i : i + 1;
            int secondPosition = reverseOrder ? 3 - i : i + 2;
            bool show = ContainsPosition(positions, firstPosition) &&
                        ContainsPosition(positions, secondPosition);

            connection.gameObject.SetActive(show);
            if (show)
                connection.color = connectionColor;
        }
    }

    private void HideAreaConnections()
    {
        HideConnections(enemyAreaConnections);
        HideConnections(allyAreaConnections);
    }

    private void HideConnections(Image[] connections)
    {
        if (connections == null)
            return;

        foreach (Image connection in connections)
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

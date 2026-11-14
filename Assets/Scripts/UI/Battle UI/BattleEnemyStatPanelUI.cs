using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 敌人属性面板。
///
/// 默认隐藏；鼠标悬停在任意存活敌人身上时显示该敌人的属性。
/// AoE 技能也只显示鼠标指到的这一名敌人。
///
/// 当玩家选中了技能（目标选择阶段）时，额外显示这个技能对当前敌人的
/// 命中率、期望伤害区间与控制几率。
/// </summary>
public class BattleEnemyStatPanelUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField, Tooltip("整个敌方面板的根节点。没有悬停目标时关闭。")]
    private GameObject panelRoot;

    [SerializeField, Tooltip("敌人名字。")]
    private TMP_Text nameText;

    [Header("攻击与防御")]
    [SerializeField] private TMP_Text physicalAttackText;
    [SerializeField] private TMP_Text physicalDefenseText;
    [SerializeField] private TMP_Text magicalAttackText;
    [SerializeField] private TMP_Text magicalDefenseText;

    [Header("其他")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text evasionText;

    [Header("技能数据（仅选中技能时显示）")]
    [SerializeField, Tooltip("技能数据行的根节点，选中技能时打开。")]
    private GameObject skillRowsRoot;

    [SerializeField, Tooltip("命中率行。")]
    private GameObject hitChanceRow;

    [SerializeField] private TMP_Text hitChanceText;

    [SerializeField, Tooltip("期望伤害行。技能没有伤害时关闭。")]
    private GameObject damageRow;

    [SerializeField] private TMP_Text damageRangeText;

    [SerializeField, Tooltip("控制几率行。技能没有控制效果时关闭。")]
    private GameObject controlRow;

    [SerializeField] private TMP_Text controlChanceText;

    private BattleController controller;
    private Camera mainCamera;
    private CanvasGroup panelCanvasGroup;

    private void Awake()
    {
        EnsurePanelCanvasGroup();
        ApplyVisibility(false);
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (controller == null)
            controller = FindAnyObjectByType<BattleController>();

        if (controller == null)
        {
            Hide();
            return;
        }

        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            mainCamera = Camera.main;

        if (mainCamera == null)
            mainCamera = FindAnyObjectByType<Camera>();

        BattleEntity hovered = FindHoveredEnemy();
        if (hovered == null)
        {
            Hide();
            return;
        }

        ApplyVisibility(true);

        FillStats(hovered);
        FillSkillRows(hovered);
    }

    /// <summary>
    /// 用 CanvasGroup 显隐，不用 SetActive：
    /// panelRoot 往往就是挂着本脚本的节点，SetActive(false) 会把脚本一起禁用，之后再也不会执行。
    /// </summary>
    private void EnsurePanelCanvasGroup()
    {
        if (panelRoot == null)
            return;

        if (panelCanvasGroup == null)
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
    }

    private void ApplyVisibility(bool visible)
    {
        EnsurePanelCanvasGroup();

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;

        // 纯信息面板，永远不拦截鼠标，避免挡住目标选择。
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    private void FillStats(BattleEntity enemy)
    {
        CharacterRuntimeData data = enemy.RuntimeData;
        if (data == null)
            return;

        StatBlock stats = data.GetTotalStats();

        SetText(nameText, data.Definition != null ? data.Definition.Name : string.Empty);
        SetValue(physicalAttackText, stats.PAtk);
        SetValue(physicalDefenseText, stats.PDef);
        SetValue(magicalAttackText, stats.MAtk);
        SetValue(magicalDefenseText, stats.MDef);
        SetValue(speedText, stats.Speed);
        SetPercent(critChanceText, data.CritChancePercent);
        SetValue(accuracyText, stats.Accuracy);
        SetValue(evasionText, stats.Evasion);
    }

    /// <summary>选中技能时才显示命中率 / 伤害 / 控制三行。</summary>
    private void FillSkillRows(BattleEntity enemy)
    {
        BattleActionContext pending = controller.IsActionInProgress ? null : controller.PendingAction;

        if (pending == null || pending.Actor == null || pending.Skill == null)
        {
            SetActive(skillRowsRoot, false);
            return;
        }

        SetActive(skillRowsRoot, true);

        int skillLevel = pending.Actor.RuntimeData != null
            ? pending.Actor.RuntimeData.GetSkillLevel(pending.Skill)
            : 1;

        // 当前没有命中判定，伤害必定生效。
        SetActive(hitChanceRow, true);
        SetText(hitChanceText, "100%");

        bool hasDamage = BattleSkillEffectExecutor.TryGetDamageRange(
            pending.Actor, enemy, pending.Skill, skillLevel, out int minDamage, out int maxDamage);

        SetActive(damageRow, hasDamage);
        if (hasDamage)
            SetText(damageRangeText, $"{minDamage} ~ {maxDamage}");

        bool hasControl = BattleSkillEffectExecutor.HasControlEffect(pending.Skill, skillLevel);
        SetActive(controlRow, hasControl);
        if (hasControl)
            SetText(controlChanceText, "100%");
    }

    private BattleEntity FindHoveredEnemy()
    {
        if (mainCamera == null || Mouse.current == null || controller == null)
            return null;

        Vector2 pointer = Mouse.current.position.ReadValue();

        IReadOnlyList<BattleEntity> all = controller.AllEntities;
        if (all == null)
            return null;

        BattleEntity closest = null;
        float closestDepth = float.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            BattleEntity entity = all[i];
            if (entity == null || entity.IsPlayer || !entity.IsAlive || entity.Unit == null)
                continue;

            if (!entity.Unit.TryGetVisualBounds(out Bounds bounds))
                continue;

            if (!TryGetScreenRect(bounds, mainCamera, out Rect rect) || !rect.Contains(pointer))
                continue;

            // 多个敌人重叠时，取离摄像机最近的那个。
            float depth = mainCamera.WorldToScreenPoint(bounds.center).z;
            if (depth < closestDepth)
            {
                closest = entity;
                closestDepth = depth;
            }
        }

        return closest;
    }

    /// <summary>把世界包围盒的八个角投影到屏幕，得到用于鼠标判定的矩形。</summary>
    private static bool TryGetScreenRect(Bounds bounds, Camera camera, out Rect rect)
    {
        rect = default;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector2 screenMin = new(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new(float.MinValue, float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            Vector3 screenCorner = camera.WorldToScreenPoint(corner);
            if (screenCorner.z <= 0f)
                return false;

            screenMin = Vector2.Min(screenMin, screenCorner);
            screenMax = Vector2.Max(screenMax, screenCorner);
        }

        rect = Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
        return true;
    }

    private void Hide()
    {
        ApplyVisibility(false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void SetValue(TMP_Text text, int value)
    {
        SetText(text, value.ToString());
    }

    private static void SetPercent(TMP_Text text, float percent)
    {
        SetText(text, $"{percent:0.#}%");
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}

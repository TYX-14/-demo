using System.Collections.Generic;
using MFrameork.Event;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗技能二级面板。
/// 监听当前行动者变化，并根据角色运行数据中的已装备技能动态生成技能条目。
/// 当前版本只展示技能信息，不执行技能或消耗 SP。
/// 建议将本控制器挂在始终激活的一级指令面板对象上，panelRoot 指向攻击按钮下的技能面板子物体。
/// </summary>
public class BattleSkillPanelController : MonoBehaviour, IEventReceiver<CurrentEntityChangedEvent>
{
    [Header("技能面板")]
    [SerializeField, Tooltip("技能二级面板根物体，建议放在攻击按钮下作为子物体")]
    private GameObject panelRoot;

    [SerializeField, Tooltip("战斗控制器，用于在每次打开技能面板时读取真正的当前行动者")]
    private BattleController battleController;

    [SerializeField, Tooltip("动态技能条目的父节点，通常是列表 Content")]
    private Transform skillListContent;

    [SerializeField, Tooltip("挂载 BattleSkillEntryUI 并绑定图标、名称、站位和 SP 文本的条目预制体")]
    private BattleSkillEntryUI skillEntryPrefab;

    [SerializeField, Tooltip("没有技能或缺少数据时显示的提示文本")]
    private TMP_Text statusText;

    private readonly List<BattleSkillEntryUI> spawnedEntries = new();
    private BattleEntity currentEntity;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<CurrentEntityChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CurrentEntityChangedEvent>(this);
    }

    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentEntity = evt.Entity;

        if (panelRoot != null && panelRoot.activeSelf)
            RefreshSkills();
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("BattleSkillPanelController 未绑定技能面板根物体。", this);
            return;
        }

        bool shouldShow = !panelRoot.activeSelf;

        if (shouldShow)
        {
            SyncCurrentEntity();
        }

        panelRoot.SetActive(shouldShow);

        if (shouldShow)
            RefreshSkills();
    }

    public void ShowPanel()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("BattleSkillPanelController 未绑定技能面板根物体。", this);
            return;
        }

        SyncCurrentEntity();
        panelRoot.SetActive(true);
        RefreshSkills();
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// 从战斗控制器读取当前行动者，避免技能面板显示上一次缓存角色的技能。
    /// </summary>
    private void SyncCurrentEntity()
    {
        if (battleController == null)
        {
            currentEntity = null;
            Debug.LogWarning("BattleSkillPanelController 未绑定 BattleController，请在 Inspector 中指定战斗控制器。", this);
            return;
        }

        currentEntity = battleController.CurrentEntity;
    }

    private void RefreshSkills()
    {
        ClearEntries();

        if (statusText != null)
            statusText.text = string.Empty;

        if (currentEntity == null || !currentEntity.IsPlayer ||
            currentEntity.Definition == null || currentEntity.RuntimeData == null)
        {
            SetStatus("无法读取当前行动角色的技能。");
            return;
        }

        if (skillListContent == null || skillEntryPrefab == null)
        {
            SetStatus("请绑定技能列表 Content 和技能条目预制体。");
            Debug.LogWarning("技能面板引用不完整：请检查 Skill List Content 与 Skill Entry Prefab。", this);
            return;
        }

        IReadOnlyList<SkillDefinitionSO> skills = currentEntity.RuntimeData.EquippedSkills;
        if (skills == null || skills.Count == 0)
        {
            SetStatus("该角色尚未装备战斗技能。");
            return;
        }

        foreach (SkillDefinitionSO skill in skills)
        {
            if (skill == null)
                continue;

            BattleSkillEntryUI entry = Instantiate(skillEntryPrefab, skillListContent);
            int actorPosition = battleController != null
                ? battleController.GetLogicalPosition(currentEntity)
                : 0;
            bool canUseFromPosition = BattleTargeting.CanUseFromPosition(skill, actorPosition);
            entry.Setup(skill, currentEntity.RuntimeData.GetSkillLevel(skill), OnSkillSelected, canUseFromPosition);
            spawnedEntries.Add(entry);
        }

        if (spawnedEntries.Count == 0)
            SetStatus("该角色尚未配置有效技能。");
    }

    private void OnSkillSelected(SkillDefinitionSO skill)
    {
        if (battleController == null)
        {
            Debug.LogWarning("BattleSkillPanelController 未绑定 BattleController，无法开始目标选择。", this);
            return;
        }

        // 进入目标选择时保留技能面板。
        // 若玩家取消选择，原面板仍然打开；只有确认目标后才由目标选择状态关闭。 
        battleController.BeginSkillTargetSelection(skill);
    }

    private void ClearEntries()
    {
        foreach (BattleSkillEntryUI entry in spawnedEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        spawnedEntries.Clear();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}

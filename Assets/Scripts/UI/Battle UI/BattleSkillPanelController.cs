using System.Collections.Generic;
using MFrameork.Event;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗技能二级面板。
/// 监听当前行动者变化，并根据角色定义动态生成技能条目。
/// 当前版本只展示技能信息，不执行技能或消耗 SP。
/// 请将本控制器挂在战斗 UI 中始终激活的父物体上，panelRoot 指向独立的技能面板子物体。
/// </summary>
public class BattleSkillPanelController : MonoBehaviour, IEventReceiver<CurrentEntityChangedEvent>
{
    [Header("技能面板")]
    [SerializeField, Tooltip("技能二级面板根物体，不应是本控制器所在的父物体")]
    private GameObject panelRoot;

    [SerializeField, Tooltip("动态技能条目的父节点，通常是列表 Content")]
    private Transform skillListContent;

    [SerializeField, Tooltip("挂载 BattleSkillEntryUI 并绑定图标、名称、站位和 SP 文本的条目预制体")]
    private BattleSkillEntryUI skillEntryPrefab;

    [SerializeField, Tooltip("没有技能或缺少数据时显示的提示文本")]
    private TMP_Text statusText;

    [SerializeField, Tooltip("返回战斗指令面板的按钮")]
    private Button closeButton;

    private readonly List<BattleSkillEntryUI> spawnedEntries = new();
    private BattleEntity currentEntity;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

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

    public void ShowPanel()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("BattleSkillPanelController 未绑定技能面板根物体。", this);
            return;
        }

        panelRoot.SetActive(true);
        RefreshSkills();
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (currentEntity != null && currentEntity.IsPlayer &&
            BattleCommandUI.Instance != null)
        {
            BattleCommandUI.Instance.ShowPanel();
        }
    }

    private void RefreshSkills()
    {
        ClearEntries();

        if (statusText != null)
            statusText.text = string.Empty;

        if (currentEntity == null || !currentEntity.IsPlayer || currentEntity.Definition == null)
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

        List<SkillDefinitionSO> skills = currentEntity.Definition.initialSkills;
        if (skills == null || skills.Count == 0)
        {
            SetStatus("该角色尚未配置技能。");
            return;
        }

        foreach (SkillDefinitionSO skill in skills)
        {
            if (skill == null)
                continue;

            BattleSkillEntryUI entry = Instantiate(skillEntryPrefab, skillListContent);
            entry.Setup(skill);
            spawnedEntries.Add(entry);
        }

        if (spawnedEntries.Count == 0)
            SetStatus("该角色尚未配置有效技能。");
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

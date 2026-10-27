using System.Collections.Generic;
using System.Linq;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 战斗 HUD 控制器。
/// 负责在 BattleStartedEvent 到来时显示战斗 UI，并在离开战斗模式时隐藏 UI。
/// Battle 模式切换本身不代表跑位已经完成，因此不能单靠 GameModeChangedEvent 显示 HUD。
/// </summary>
public class BattleHUDcontroller : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<CurrentEntityChangedEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("战斗 HUD 根节点")]
    [SerializeField, Tooltip("英雄状态栏列表所在的根物体")]
    private GameObject healthBarPanelRoot;

    [Header("CTB 显隐")]
    [SerializeField, Tooltip("Battle UI 下的 CTB 根节点，由 BattleHUDcontroller 统一控制显隐。")]
    private GameObject ctbRoot;

    [Header("英雄状态栏生成")]
    [SerializeField, Tooltip("仅用于我方英雄的状态栏预制体")]
    private HealthBarController healthBarPrefab;

    [SerializeField, Tooltip("状态栏排列容器。建议添加 Vertical Layout Group；未指定时使用 Health Bar Panel Root。")]
    private Transform healthBarContainer;

    private readonly List<HealthBarController> spawnedHealthBars = new();
    private BattleEntity currentActingEntity;
    private bool battleUIStarted;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<CurrentEntityChangedEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);

        battleUIStarted = false;
        SetBattleUIVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<CurrentEntityChangedEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    /// <summary>
    /// 只在 BattleSetupState 确认入场跑位完成后显示战斗 HUD。
    /// </summary>
    public void OnEvent(BattleStartedEvent evt)
    {
        battleUIStarted = true;
        SetBattleUIVisible(true);
        BuildAllyHealthBars();
        RefreshActingHighlight();
    }

    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentActingEntity = evt.Entity;
        SortHealthBarsByPosition();
        RefreshActingHighlight();
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode != GameMode.Battle)
        {
            battleUIStarted = false;
            currentActingEntity = null;
            SetBattleUIVisible(false);
            ClearHealthBars();
            return;
        }

        // 切入战斗模式后仍需等待跑位完成事件，不能在这里提前显示 CTB。
        if (!battleUIStarted)
            SetBattleUIVisible(false);
    }

    private void SetBattleUIVisible(bool visible)
    {
        if (healthBarPanelRoot != null && healthBarPanelRoot.activeSelf != visible)
            healthBarPanelRoot.SetActive(visible);
        if (ctbRoot != null && ctbRoot.activeSelf != visible)
            ctbRoot.SetActive(visible);
    }

    private void BuildAllyHealthBars()
    {
        ClearHealthBars();

        if (healthBarPrefab == null)
        {
            Debug.LogError("BattleHUDcontroller：未指定 Hero Health Bar Prefab，无法生成英雄状态栏。", this);
            return;
        }

        Transform container = healthBarContainer != null
            ? healthBarContainer
            : healthBarPanelRoot != null ? healthBarPanelRoot.transform : null;

        if (container == null)
        {
            Debug.LogError("BattleHUDcontroller：未指定状态栏容器，也找不到 Health Bar Panel Root。", this);
            return;
        }

        BattleController battleController = FindAnyObjectByType<BattleController>();
        if (battleController == null)
        {
            Debug.LogError("BattleHUDcontroller：找不到 BattleController，无法读取本场友军。", this);
            return;
        }

        List<BattleEntity> allies = battleController.AllEntities
            .Where(entity => entity != null && entity.IsPlayer && entity.RuntimeData != null)
            .OrderBy(GetPosition)
            .ToList();

        foreach (BattleEntity ally in allies)
        {
            HealthBarController bar = Instantiate(healthBarPrefab, container, false);
            bar.Bind(ally.RuntimeData);
            bar.SetActing(ally == currentActingEntity);
            spawnedHealthBars.Add(bar);
        }

        SortHealthBarsByPosition();
    }

    private void ClearHealthBars()
    {
        foreach (HealthBarController bar in spawnedHealthBars)
        {
            if (bar != null)
                Destroy(bar.gameObject);
        }
        spawnedHealthBars.Clear();
    }

    private void SortHealthBarsByPosition()
    {
        spawnedHealthBars.RemoveAll(bar => bar == null);
        spawnedHealthBars.Sort((a, b) =>
        {
            int positionA = a.BoundCharacter != null ? a.BoundCharacter.Position : int.MaxValue;
            int positionB = b.BoundCharacter != null ? b.BoundCharacter.Position : int.MaxValue;
            return positionA.CompareTo(positionB);
        });

        for (int i = 0; i < spawnedHealthBars.Count; i++)
            spawnedHealthBars[i].transform.SetSiblingIndex(i);
    }

    private void RefreshActingHighlight()
    {
        foreach (HealthBarController bar in spawnedHealthBars)
        {
            if (bar == null)
                continue;

            bool isActing = currentActingEntity != null &&
                            currentActingEntity.IsPlayer &&
                            bar.BoundCharacter == currentActingEntity.RuntimeData;
            bar.SetActing(isActing);
        }
    }

    private static int GetPosition(BattleEntity entity)
    {
        int position = entity.RuntimeData != null ? entity.RuntimeData.Position : 0;
        return position > 0 ? position : int.MaxValue;
    }
}

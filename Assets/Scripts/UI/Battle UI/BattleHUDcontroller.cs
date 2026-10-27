using System.Collections.Generic;
using System.Linq;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 战斗 HUD 控制器。
/// 负责控制战斗状态栏列表与 CTB UI 的显隐，并将每条状态栏绑定到一名友军。
/// 状态栏位于屏幕 UI 中，不跟随战场角色的世界坐标。
/// </summary>
public class BattleHUDcontroller : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<CurrentEntityChangedEvent>
{
    [Header("战斗 HUD 根节点")]
    [SerializeField, Tooltip("英雄状态栏列表所在的根物体")]
    private GameObject healthBarPanelRoot;

    [SerializeField, Tooltip("CTB UI 的根物体")]
    private GameObject ctbRoot;


    [Header("英雄状态栏生成")]
    [SerializeField, Tooltip("仅用于我方英雄的状态栏预制体")]
    private HealthBarController healthBarPrefab;

    [SerializeField, Tooltip("状态栏排列容器。建议添加 Vertical Layout Group；未指定时使用 Health Bar Panel Root。")]
    private Transform healthBarContainer;

    private readonly List<HealthBarController> spawnedHealthBars = new();
    private BattleEntity currentActingEntity;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<CurrentEntityChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<CurrentEntityChangedEvent>(this);
    }

    /// <summary>
    /// 战斗正式开始、友军入场跑位结束后，生成我方英雄状态栏。
    /// </summary>
    public void OnEvent(BattleStartedEvent evt)
    {
        SetupHUD();
        BuildAllyHealthBars();
        RefreshActingHighlight();
    }

    /// <summary>
    /// 回合分发器切换行动者时，更新状态栏高亮和站位排序。
    /// </summary>
    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentActingEntity = evt.Entity;
        SortHealthBarsByPosition();
        RefreshActingHighlight();
    }

    private void SetupHUD()
    {
        if (healthBarPanelRoot != null)
            healthBarPanelRoot.SetActive(true);

        if (ctbRoot != null)
            ctbRoot.SetActive(true);
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
            .OrderBy(entity => GetPosition(entity))
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

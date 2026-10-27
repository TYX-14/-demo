using System.Collections.Generic;
using System.Linq;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// CTB 时间轴控制器。
/// 独立负责时间轴头像的创建、实体绑定、初始排序和当前行动者高亮。
/// 单个头像的显示由 TimelineIconController 负责；阵营背景沿用 Timeline Icon 预制体自身配置。
/// </summary>
public sealed class CTBController : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<CurrentEntityChangedEvent>
{
    [Header("CTB 根节点")]
    [SerializeField, Tooltip("CTB UI 根物体，用于战斗开始时显示 CTB。此脚本应挂在不会随该根节点一起禁用的活动物体上。")]
    private GameObject ctbRoot;

    [Header("CTB 时间轴")]
    [SerializeField, Tooltip("Timeline Icon 预制体，阵营背景应直接配置在该预制体上。")]
    private GameObject timelineIconPrefab;

    [SerializeField, Tooltip("CTB 内专门排列时间轴头像的容器。建议使用 Horizontal Layout Group。")]
    private Transform timelineIconContainer;

    private readonly List<TimelineIconController> spawnedIcons = new();
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
        ClearTimelineIcons();
    }

    public void OnEvent(BattleStartedEvent evt)
    {
        if (ctbRoot != null)
            ctbRoot.SetActive(true);

        BuildTimelineIcons();
        RefreshActingHighlight();
    }

    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentActingEntity = evt.Entity;
        RefreshActingHighlight();
    }

    /// <summary>
    /// 根据当前战斗实体建立初始时间轴。
    /// 当前行动系统仍是临时的速度优先选择，因此初始顺序按速度降序、再按位置排序。
    /// </summary>
    public void BuildTimelineIcons()
    {
        ClearTimelineIcons();

        if (timelineIconPrefab == null)
        {
            Debug.LogError("CTBController：未指定 Timeline Icon Prefab。", this);
            return;
        }

        if (timelineIconContainer == null)
        {
            Debug.LogError("CTBController：未指定 Timeline Icon Container。请指定 CTB 内负责排列头像的子物体。", this);
            return;
        }

        BattleController battleController = FindAnyObjectByType<BattleController>();
        if (battleController == null)
        {
            Debug.LogError("CTBController：找不到 BattleController，无法建立时间轴。", this);
            return;
        }

        List<BattleEntity> entities = battleController.AllEntities
            .Where(entity => entity != null && entity.RuntimeData != null && entity.Definition != null)
            .OrderByDescending(entity => entity.TotalStats.Speed)
            .ThenBy(entity => GetPositionPriority(entity, battleController.AllEntities))
            .ToList();

        foreach (BattleEntity entity in entities)
        {
            GameObject iconObject = Instantiate(timelineIconPrefab, timelineIconContainer, false);
            TimelineIconController icon = iconObject.GetComponent<TimelineIconController>();
            if (icon == null)
                icon = iconObject.AddComponent<TimelineIconController>();

            icon.Bind(entity);
            icon.SetActing(entity == currentActingEntity);
            spawnedIcons.Add(icon);
        }
    }

    private void ClearTimelineIcons()
    {
        foreach (TimelineIconController icon in spawnedIcons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }

        spawnedIcons.Clear();
    }

    private void RefreshActingHighlight()
    {
        foreach (TimelineIconController icon in spawnedIcons)
        {
            if (icon != null)
                icon.SetActing(icon.BoundEntity != null && icon.BoundEntity == currentActingEntity);
        }
    }

    private static int GetPositionPriority(BattleEntity entity, IReadOnlyList<BattleEntity> allEntities)
    {
        if (entity?.RuntimeData != null && entity.RuntimeData.Position > 0)
            return entity.RuntimeData.Position;

        int sideOrder = 1;
        if (allEntities == null)
            return sideOrder;

        for (int i = 0; i < allEntities.Count; i++)
        {
            BattleEntity previous = allEntities[i];
            if (previous == entity)
                break;

            if (previous != null && entity != null && previous.IsPlayer == entity.IsPlayer)
                sideOrder++;
        }

        return sideOrder;
    }
}

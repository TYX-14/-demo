using System.Collections.Generic;
using MFrameork.Event;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CTB 显示控制器。
/// 当前行动者使用场景中已有的大头像 Image；两个后续行动池使用 Timeline Icon 预制体。
/// 本脚本不计算行动顺序，后续由 CTB 行动顺序系统调用对应的 Set 方法填充池子。
/// </summary>
public sealed class CTBController : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<CurrentEntityChangedEvent>,
    IEventReceiver<CTBQueueUpdatedEvent>
{
    [Header("CTB 根节点")]
    [SerializeField, Tooltip("CTB UI 根物体。此脚本应挂在不会随该根节点一起禁用的活动物体上。")]
    private GameObject ctbRoot;

    [Header("当前行动者")]
    [SerializeField, Tooltip("场景中已有的大头像 Image，不需要制作头像预制体。")]
    private Image currentActingPortraitImage;

    [Header("当前回合后续行动池")]
    [SerializeField, Tooltip("显示本回合当前行动者之后的行动者。顺序由外部行动顺序系统传入。")]
    private Transform currentRoundUpcomingContainer;

    [Header("下一回合行动池")]
    [SerializeField, Tooltip("显示下一回合行动顺序。当前不自动计算，由后续行动顺序系统传入。")]
    private Transform nextRoundContainer;

    [Header("后续行动头像")]
    [SerializeField, Tooltip("Timeline Icon 预制体。友方/敌方背景由预制体上的 TimelineIconController 切换。")]
    private GameObject timelineIconPrefab;

    private readonly List<TimelineIconController> currentRoundIcons = new();
    private readonly List<TimelineIconController> nextRoundIcons = new();
    private BattleEntity currentActingEntity;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<CurrentEntityChangedEvent>(this);
        EventBus.Subscribe<CTBQueueUpdatedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<CurrentEntityChangedEvent>(this);
        EventBus.Unsubscribe<CTBQueueUpdatedEvent>(this);
        ClearIcons(currentRoundIcons);
        ClearIcons(nextRoundIcons);
    }

    public void OnEvent(BattleStartedEvent evt)
    {
        if (ctbRoot != null)
            ctbRoot.SetActive(true);

        currentActingEntity = null;
        RefreshCurrentActingPortrait();
        ClearIcons(currentRoundIcons);
        ClearIcons(nextRoundIcons);
    }

    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentActingEntity = evt.Entity;
        RefreshCurrentActingPortrait();
    }

    public void OnEvent(CTBQueueUpdatedEvent evt)
    {
        SetCurrentRoundUpcomingEntities(evt.CurrentRoundNodes);
        SetNextRoundEntities(evt.NextRoundNodes);
    }

    private void RefreshCurrentActingPortrait()
    {
        if (currentActingPortraitImage == null)
            return;

        Sprite portrait = currentActingEntity != null && currentActingEntity.Definition != null
            ? currentActingEntity.Definition.LeftBattleIdle
            : null;

        currentActingPortraitImage.sprite = portrait;
        currentActingPortraitImage.enabled = portrait != null;
    }

    /// <summary>
    /// 由行动顺序系统传入当前回合尚待行动的实体。此方法不自行排序或计算行动顺序。
    /// </summary>
    public void SetCurrentRoundUpcomingEntities(IEnumerable<CTBDisplayNode> entities)
    {
        PopulatePool(entities, currentRoundUpcomingContainer, currentRoundIcons);
    }

    /// <summary>
    /// 由行动顺序系统传入下一回合的实体顺序。当前阶段不自行计算下一回合顺序。
    /// </summary>
    public void SetNextRoundEntities(IEnumerable<CTBDisplayNode> entities)
    {
        PopulatePool(entities, nextRoundContainer, nextRoundIcons);
    }

    public void ClearCurrentRoundUpcomingPool() => ClearIcons(currentRoundIcons);
    public void ClearNextRoundPool() => ClearIcons(nextRoundIcons);

    private void PopulatePool(
        IEnumerable<CTBDisplayNode> entities,
        Transform container,
        List<TimelineIconController> destination)
    {
        ClearIcons(destination);

        if (entities == null)
            return;

        if (container == null)
        {
            Debug.LogError("CTBController：未指定对应的头像池容器。", this);
            return;
        }

        if (timelineIconPrefab == null)
        {
            Debug.LogError("CTBController：未指定 Timeline Icon Prefab。", this);
            return;
        }

        foreach (CTBDisplayNode node in entities)
        {
            if (node.Entity == null)
                continue;

            GameObject iconObject = Instantiate(timelineIconPrefab, container, false);
            TimelineIconController icon = iconObject.GetComponent<TimelineIconController>();
            if (icon == null)
            {
                Debug.LogError("CTBController：Timeline Icon 预制体缺少 TimelineIconController 组件。请将组件添加到预制体上。", iconObject);
                Destroy(iconObject);
                continue;
            }

            icon.Bind(node);
            destination.Add(icon);
        }
    }

    private static void ClearIcons(List<TimelineIconController> icons)
    {
        foreach (TimelineIconController icon in icons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }

        icons.Clear();
    }
}

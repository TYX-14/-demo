using System.Collections;
using System.Collections.Generic;
using MFrameork.Event;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CTB 展示控制器。行动顺序由 BattleRoundScheduler 提供；本类只负责头像复用、队列过渡与大头像动画。
/// </summary>
public sealed class CTBController : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<CurrentEntityChangedEvent>,
    IEventReceiver<CTBQueueUpdatedEvent>,
    IEventReceiver<BattleRoundChangedEvent>
{
    [Header("当前行动者")]
    [SerializeField, Tooltip("场景中已有的大头像 Image，不需要制作头像预制体。")]
    private Image currentActingPortraitImage;
    [SerializeField, Tooltip("当前行动者大头像下方显示实体名称的 TMP_Text。")]
    private TMP_Text currentActingNameText;

    [Header("当前回合后续行动池")]
    [SerializeField] private Transform currentRoundUpcomingContainer;
    [Header("下一回合行动池")]
    [SerializeField] private Transform nextRoundContainer;

    [Header("后续行动头像")]
    [SerializeField] private GameObject timelineIconPrefab;
    [SerializeField, Min(0f)] private float enterDuration = 0.22f;
    [SerializeField, Min(0f)] private float exitDuration = 0.15f;
    [SerializeField, Min(0f)] private float moveDuration = 0.22f;
    [SerializeField, Min(0f), Tooltip("新一轮队首小头像飞入大头像前的快速交接时长。")]
    private float roundHandoffDuration = 0.18f;
    [SerializeField, Min(0f)] private float iconEnterOffset = 18f;
    [SerializeField, Min(0f)] private float iconExitOffset = 14f;
    [SerializeField, Min(0f)] private float portraitSlideDistance = 90f;

    private readonly List<TimelineIconController> currentRoundIcons = new();
    private readonly List<TimelineIconController> nextRoundIcons = new();
    private TimelineIconPool iconPool;
    private readonly HashSet<TimelineIconController> exitingIcons = new();

    private BattleEntity currentActingEntity;
    private bool isOpeningSequence = true;
    private Coroutine portraitRoutine;
    private Coroutine openingRoutine;
    private Coroutine roundHandoffRoutine;
    private bool roundTransitionPending;
    private Vector2 portraitRestingPosition;
    private float portraitRestingAlpha = 1f;
    private bool portraitPositionCached;

    private void Awake()
    {
        CachePortraitRestingPosition();
        iconPool = GetComponent<TimelineIconPool>();
        if (iconPool == null)
            iconPool = gameObject.AddComponent<TimelineIconPool>();
        iconPool.Initialize(timelineIconPrefab, transform);
    }

    private void CachePortraitRestingPosition()
    {
        if (portraitPositionCached || currentActingPortraitImage == null)
            return;
        portraitRestingPosition = currentActingPortraitImage.rectTransform.anchoredPosition;
        // 固定大头像的目标透明度，不能从正在执行/被中断的过渡动画读取，否则透明度会逐次衰减。
        portraitRestingAlpha = 1f;
        portraitPositionCached = true;
    }

    private void OnEnable()
    {
        // 如果上次禁用时动画被中断，等父级重新激活后再回收图标。
        // 不能在 OnDisable 中回收：此时 CTB 的父容器可能正在停用，
        // TimelineIconPool.Release() 的 SetParent 会触发 Unity 错误。
        RecycleAllVisibleIcons();

        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<CurrentEntityChangedEvent>(this);
        EventBus.Subscribe<CTBQueueUpdatedEvent>(this);
        EventBus.Subscribe<BattleRoundChangedEvent>(this);
        isOpeningSequence = true;
        currentActingEntity = null;
        RefreshCurrentActingName();
        SetPortraitBlank();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<CurrentEntityChangedEvent>(this);
        EventBus.Unsubscribe<CTBQueueUpdatedEvent>(this);
        EventBus.Unsubscribe<BattleRoundChangedEvent>(this);
        StopAllCoroutines();
        portraitRoutine = null;
        openingRoutine = null;
        roundHandoffRoutine = null;
        roundTransitionPending = false;

        // 此处只停止动画，不移动/停用头像对象。
        // 当父级正在停用时，Unity 不允许在这里重设子对象的 parent。
        // 图标列表保留到下一次 OnEnable，由上方在层级恢复激活后统一回收。
        isOpeningSequence = true;
        currentActingEntity = null;
        SetPortraitBlank();
        RefreshCurrentActingName();
    }

    /// <summary>战斗 UI 显示事件到来时，大头像先保持空白，等待真正选出第一名行动者。</summary>
    public void OnEvent(BattleStartedEvent evt)
    {
        isOpeningSequence = true;
        currentActingEntity = null;
        if (openingRoutine != null)
            StopCoroutine(openingRoutine);
        openingRoutine = null;
        if (portraitRoutine != null)
            StopCoroutine(portraitRoutine);
        portraitRoutine = null;
        RecycleAllVisibleIcons();
        SetPortraitBlank();
        RefreshCurrentActingName();
    }

    public void OnEvent(BattleRoundChangedEvent evt)
    {
        // 此事件先于行动者变更发布，等待队列快照到达后再开始头像交接。
        roundTransitionPending = true;
    }

    public void OnEvent(CurrentEntityChangedEvent evt)
    {
        currentActingEntity = evt.Entity;
        RefreshCurrentActingName();

        // 首次显示或跨轮交接时，先等队列快照，不能立即切换大头像。
        if (isOpeningSequence || roundTransitionPending)
            return;

        if (portraitRoutine != null)
            StopCoroutine(portraitRoutine);
        portraitRoutine = StartCoroutine(TransitionPortraitAndNotify(currentActingEntity, false));
    }

    public void OnEvent(CTBQueueUpdatedEvent evt)
    {
        if (roundTransitionPending && currentActingEntity != null)
        {
            TimelineIconController handoffIcon = TakeRoundHeadIcon(currentActingEntity);
            SyncPool(evt.CurrentRoundNodes, currentRoundUpcomingContainer, currentRoundIcons);
            roundTransitionPending = false;

            if (roundHandoffRoutine != null)
                StopCoroutine(roundHandoffRoutine);
            // 下一轮头像池的刷新延后到大头像显现之后，避免新头像入场动画与交接动画同时发生。
            roundHandoffRoutine = StartCoroutine(
                PlayRoundHandoffSequence(currentActingEntity, handoffIcon, evt.NextRoundNodes));
            return;
        }

        if (isOpeningSequence && currentActingEntity != null)
        {
            CTBDisplayNode[] openingNodes = PrependCurrentActor(currentActingEntity, evt.CurrentRoundNodes);
            SyncPool(openingNodes, currentRoundUpcomingContainer, currentRoundIcons);
            SyncPool(evt.NextRoundNodes, nextRoundContainer, nextRoundIcons);

            if (openingRoutine != null)
                StopCoroutine(openingRoutine);
            openingRoutine = StartCoroutine(PlayOpeningSequence());
            return;
        }

        SyncPool(evt.CurrentRoundNodes, currentRoundUpcomingContainer, currentRoundIcons);
        SyncPool(evt.NextRoundNodes, nextRoundContainer, nextRoundIcons);
    }

    private TimelineIconController TakeRoundHeadIcon(BattleEntity actor)
    {
        if (actor == null) return null;
        for (int i = 0; i < nextRoundIcons.Count; i++)
        {
            TimelineIconController icon = nextRoundIcons[i];
            if (icon == null || icon.BoundEntityId != actor.ID) continue;
            nextRoundIcons.RemoveAt(i);
            icon.SetIgnoredByLayout(true);
            exitingIcons.Add(icon);
            ForceLayout(nextRoundContainer);
            return icon;
        }
        return null;
    }

    private IEnumerator PlayRoundHandoffSequence(BattleEntity actor, TimelineIconController icon, CTBDisplayNode[] nextRoundNodes)
    {
        SetPortraitBlank();
        if (icon != null)
        {
            Vector3 startPosition = icon.transform.position;
            Vector3 targetPosition = currentActingPortraitImage != null ? currentActingPortraitImage.rectTransform.position : startPosition;
            icon.transform.SetParent(transform, true);
            icon.transform.SetAsLastSibling();
            float duration = roundHandoffDuration;
            if (duration <= 0f) icon.transform.position = targetPosition;
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    icon.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                    yield return null;
                }
                icon.transform.position = targetPosition;
            }
            exitingIcons.Remove(icon);
            ReturnIcon(icon);
        }
        // 小头像抵达并回收后，大头像才显现。
        yield return TransitionPortrait(actor, true);
        if (currentActingEntity == actor)
            EventBus.Publish(new CTBDisplayedActorChangedEvent(actor));

        // 新一轮大头像已显现，再生成/复用下一轮小头像并播放既有的入场动画。
        SyncPool(nextRoundNodes, nextRoundContainer, nextRoundIcons);
        roundHandoffRoutine = null;
    }
    public void SetCurrentRoundUpcomingEntities(IEnumerable<CTBDisplayNode> entities) =>
        SyncPool(ToArray(entities), currentRoundUpcomingContainer, currentRoundIcons);

    public void SetNextRoundEntities(IEnumerable<CTBDisplayNode> entities) =>
        SyncPool(ToArray(entities), nextRoundContainer, nextRoundIcons);

    public void ClearCurrentRoundUpcomingPool() => RecyclePool(currentRoundIcons);
    public void ClearNextRoundPool() => RecyclePool(nextRoundIcons);

    private IEnumerator PlayOpeningSequence()
    {
        // 先让当前行动者以普通小头像入场，随后立即向左退出并由大头像接棒。
        if (enterDuration > 0f)
            yield return new WaitForSecondsRealtime(enterDuration);

        if (currentRoundIcons.Count > 0 &&
            currentRoundIcons[0] != null &&
            currentRoundIcons[0].BoundEntity == currentActingEntity)
        {
            TimelineIconController actorIcon = currentRoundIcons[0];
            currentRoundIcons.RemoveAt(0);
            actorIcon.SetIgnoredByLayout(true);
            float step = GetSlotStep(currentRoundUpcomingContainer, actorIcon);
            for (int i = 0; i < currentRoundIcons.Count; i++)
            {
                TimelineIconController icon = currentRoundIcons[i];
                if (icon == null)
                    continue;
                icon.transform.SetSiblingIndex(i);
                icon.SetVisualOffset(new Vector2(step, 0f));
                StartCoroutine(icon.PlayMoveAnimation(new Vector2(step, 0f), moveDuration));
            }

            ForceLayout(currentRoundUpcomingContainer);
            exitingIcons.Add(actorIcon);
            StartCoroutine(ExitAndRecycle(actorIcon));
        }

        yield return TransitionPortrait(currentActingEntity, true);
        isOpeningSequence = false;
        EventBus.Publish(new CTBDisplayedActorChangedEvent(currentActingEntity));
        openingRoutine = null;
    }

    private IEnumerator TransitionPortraitAndNotify(BattleEntity nextEntity, bool opening)
    {
        yield return TransitionPortrait(nextEntity, opening);

        // 如果动画期间已经切换到另一位行动者，旧动画不得覆盖新行动者的 UI 状态。
        if (currentActingEntity == nextEntity)
            EventBus.Publish(new CTBDisplayedActorChangedEvent(nextEntity));

        portraitRoutine = null;
    }

    private IEnumerator TransitionPortrait(BattleEntity nextEntity, bool opening)
    {
        if (currentActingPortraitImage == null)
            yield break;

        CachePortraitRestingPosition();
        RectTransform imageRect = currentActingPortraitImage.rectTransform;
        Vector2 restingPosition = portraitRestingPosition;
        Color color = currentActingPortraitImage.color;

        if (!opening && currentActingPortraitImage.enabled && currentActingPortraitImage.sprite != null)
        {
            yield return AnimatePortrait(imageRect, restingPosition + Vector2.left * portraitSlideDistance,
                0f, moveDuration * 0.5f, portraitRestingAlpha);
        }

        Sprite nextSprite = nextEntity != null && nextEntity.Definition != null
            ? nextEntity.Definition.LeftBattleIdle
            : null;

        currentActingPortraitImage.sprite = nextSprite;
        currentActingPortraitImage.enabled = nextSprite != null;
        if (nextSprite == null)
        {
            imageRect.anchoredPosition = restingPosition;
            currentActingPortraitImage.color = new Color(color.r, color.g, color.b, color.a);
            yield break;
        }

        imageRect.anchoredPosition = restingPosition + Vector2.right * portraitSlideDistance;
        currentActingPortraitImage.color = new Color(color.r, color.g, color.b, 0f);
        yield return AnimatePortrait(imageRect, restingPosition, portraitRestingAlpha, moveDuration, portraitRestingAlpha);
        portraitRoutine = null;
    }

    private IEnumerator AnimatePortrait(RectTransform imageRect, Vector2 targetPosition, float targetAlpha, float duration, float finalAlpha)
    {
        Vector2 startPosition = imageRect.anchoredPosition;
        float startAlpha = currentActingPortraitImage.color.a;
        Color color = currentActingPortraitImage.color;

        if (duration <= 0f)
        {
            imageRect.anchoredPosition = targetPosition;
            currentActingPortraitImage.color = new Color(color.r, color.g, color.b, targetAlpha > 0f ? finalAlpha : 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            imageRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
            currentActingPortraitImage.color = new Color(
                color.r, color.g, color.b, Mathf.Lerp(startAlpha, targetAlpha > 0f ? finalAlpha : 0f, eased));
            yield return null;
        }

        imageRect.anchoredPosition = targetPosition;
        currentActingPortraitImage.color = new Color(color.r, color.g, color.b, targetAlpha > 0f ? finalAlpha : 0f);
    }

    private void SetPortraitBlank()
    {
        if (currentActingPortraitImage == null)
            return;

        CachePortraitRestingPosition();
        currentActingPortraitImage.sprite = null;
        currentActingPortraitImage.enabled = false;
        currentActingPortraitImage.rectTransform.anchoredPosition = portraitRestingPosition;
        Color color = currentActingPortraitImage.color;
        currentActingPortraitImage.color = new Color(color.r, color.g, color.b, 1f);
    }

    private void RefreshCurrentActingName()
    {
        if (currentActingNameText != null)
            currentActingNameText.text = GetEntityDisplayName(currentActingEntity);
    }

    private static string GetEntityDisplayName(BattleEntity entity)
    {
        if (entity == null)
            return string.Empty;
        if (entity.RuntimeData != null && !string.IsNullOrWhiteSpace(entity.RuntimeData.DisplayName))
            return entity.RuntimeData.DisplayName;
        if (entity.Definition != null && !string.IsNullOrWhiteSpace(entity.Definition.Name))
            return entity.Definition.Name;
        return entity.ID ?? string.Empty;
    }

    private void SyncPool(CTBDisplayNode[] nodes, Transform container, List<TimelineIconController> destination)
    {
        if (container == null || timelineIconPrefab == null)
        {
            if (container == null)
                Debug.LogError("CTBController：未指定对应的头像池容器。", this);
            if (timelineIconPrefab == null)
                Debug.LogError("CTBController：未指定 Timeline Icon Prefab。", this);
            return;
        }

        nodes ??= System.Array.Empty<CTBDisplayNode>();
        var oldIcons = new List<TimelineIconController>(destination);
        var oldIndices = new Dictionary<string, int>();
        var available = new Dictionary<string, TimelineIconController>();
        for (int i = 0; i < oldIcons.Count; i++)
        {
            TimelineIconController icon = oldIcons[i];
            if (icon == null || string.IsNullOrEmpty(icon.BoundEntityId))
                continue;
            available[icon.BoundEntityId] = icon;
            oldIndices[icon.BoundEntityId] = i;
        }

        // 先把不再出现在队列中的头像标记为忽略布局，让剩余头像立即获得新槽位。
        var desiredIds = new HashSet<string>();
        foreach (CTBDisplayNode node in nodes)
            if (node.Entity != null)
                desiredIds.Add(node.EntityId);

        foreach (TimelineIconController oldIcon in oldIcons)
        {
            if (oldIcon == null || desiredIds.Contains(oldIcon.BoundEntityId))
                continue;
            oldIcon.SetIgnoredByLayout(true);
            exitingIcons.Add(oldIcon);
            StartCoroutine(ExitAndRecycle(oldIcon));
        }

        var nextIcons = new List<TimelineIconController>(nodes.Length);
        var moved = new List<(TimelineIconController icon, Vector2 offset)>();
        var entering = new List<TimelineIconController>();
        float step = GetSlotStep(container, null);

        for (int i = 0; i < nodes.Length; i++)
        {
            CTBDisplayNode node = nodes[i];
            if (node.Entity == null)
                continue;

            TimelineIconController icon;
            if (available.TryGetValue(node.EntityId, out icon))
            {
                available.Remove(node.EntityId);
                int oldIndex = oldIndices[node.EntityId];
                icon.Bind(node);
                icon.transform.SetParent(container, false);
                icon.SetIgnoredByLayout(false);
                icon.transform.SetSiblingIndex(i);
                Vector2 offset = new Vector2((oldIndex - i) * step, 0f);
                if (oldIndex != i)
                    moved.Add((icon, offset));
            }
            else
            {
                icon = GetIcon(container);
                if (icon == null)
                    continue;
                icon.Bind(node);
                icon.transform.SetSiblingIndex(i);
                entering.Add(icon);
            }

            nextIcons.Add(icon);
        }

        destination.Clear();
        destination.AddRange(nextIcons);
        ForceLayout(container);

        foreach (var movement in moved)
        {
            movement.icon.SetVisualOffset(movement.offset);
            StartCoroutine(movement.icon.PlayMoveAnimation(movement.offset, moveDuration));
        }

        foreach (TimelineIconController icon in entering)
            StartCoroutine(icon.PlayEnterAnimation(iconEnterOffset, enterDuration));
    }

    private TimelineIconController GetIcon(Transform container)
    {
        return iconPool != null ? iconPool.Get(container) : null;
    }

    private IEnumerator ExitAndRecycle(TimelineIconController icon)
    {
        if (icon != null)
            yield return icon.PlayExitAnimation(-iconExitOffset, exitDuration);

        if (icon != null)
        {
            exitingIcons.Remove(icon);
            ReturnIcon(icon);
        }
    }

    private void ReturnIcon(TimelineIconController icon)
    {
        if (iconPool != null)
            iconPool.Release(icon);
    }

    private void RecyclePool(List<TimelineIconController> icons)
    {
        foreach (TimelineIconController icon in icons)
            ReturnIcon(icon);
        icons.Clear();
    }

    private void RecycleAllVisibleIcons()
    {
        var all = new HashSet<TimelineIconController>();
        foreach (TimelineIconController icon in currentRoundIcons)
            if (icon != null) all.Add(icon);
        foreach (TimelineIconController icon in nextRoundIcons)
            if (icon != null) all.Add(icon);
        foreach (TimelineIconController icon in exitingIcons)
            if (icon != null) all.Add(icon);

        currentRoundIcons.Clear();
        nextRoundIcons.Clear();
        exitingIcons.Clear();
        foreach (TimelineIconController icon in all)
            ReturnIcon(icon);
    }

    private float GetSlotStep(Transform container, TimelineIconController fallback)
    {
        float width = 100f;
        if (fallback != null && fallback.transform is RectTransform fallbackRect && fallbackRect.rect.width > 0f)
            width = fallbackRect.rect.width;
        else if (container != null && container.childCount > 0 &&
                 container.GetChild(0) is RectTransform childRect && childRect.rect.width > 0f)
            width = childRect.rect.width;

        HorizontalLayoutGroup layout = container != null ? container.GetComponent<HorizontalLayoutGroup>() : null;
        return width + (layout != null ? layout.spacing : 0f);
    }

    private static void ForceLayout(Transform container)
    {
        if (container is RectTransform rect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static CTBDisplayNode[] PrependCurrentActor(BattleEntity actor, CTBDisplayNode[] remaining)
    {
        int remainingCount = remaining != null ? remaining.Length : 0;
        var result = new CTBDisplayNode[remainingCount + 1];
        result[0] = new CTBDisplayNode(actor);
        if (remainingCount > 0)
            System.Array.Copy(remaining, 0, result, 1, remainingCount);
        return result;
    }

    private static CTBDisplayNode[] ToArray(IEnumerable<CTBDisplayNode> nodes)
    {
        if (nodes == null)
            return System.Array.Empty<CTBDisplayNode>();
        return nodes is CTBDisplayNode[] array ? array : new List<CTBDisplayNode>(nodes).ToArray();
    }
}

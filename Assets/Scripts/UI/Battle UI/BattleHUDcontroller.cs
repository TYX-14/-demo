using System.Collections;
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
    IEventReceiver<CTBDisplayedActorChangedEvent>,
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

    [Header("Health Bar 入场动画")]
    [SerializeField, Min(0.01f), Tooltip("英雄状态栏在战斗开始时渐显的时长。")]
    private float healthBarFadeDuration = 0.25f;

    private readonly List<HealthBarController> spawnedHealthBars = new();
    private BattleEntity displayedActingEntity;
    private CanvasGroup healthBarCanvasGroup;
    private Coroutine healthBarFadeCoroutine;
    private bool battleUIStarted;

    private void Awake()
    {
        ResolveHealthBarCanvasGroup();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);

        battleUIStarted = false;
        SetBattleUIVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);

        if (healthBarFadeCoroutine != null)
        {
            StopCoroutine(healthBarFadeCoroutine);
            healthBarFadeCoroutine = null;
        }
    }

    /// <summary>
    /// 只在 BattleSetupState 确认入场跑位完成后显示战斗 HUD。
    /// </summary>
    public void OnEvent(BattleStartedEvent evt)
    {
        battleUIStarted = true;
        SetBattleUIVisible(true);
        BuildAllyHealthBars();
        StartHealthBarFadeIn();
        RefreshActingHighlight();
    }

    public void OnEvent(CTBDisplayedActorChangedEvent evt)
    {
        displayedActingEntity = evt.Entity;
        RefreshActingHighlight();
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode != GameMode.Battle)
        {
            battleUIStarted = false;
            displayedActingEntity = null;
            SetBattleUIVisible(false);
            ClearHealthBars();
            return;
        }

        // 切入战斗模式后仍需等待跑位完成事件，不能在这里提前显示 CTB。
        if (!battleUIStarted)
            SetBattleUIVisible(false);
    }

    /// <summary>特写期间是否强制隐藏 HUD。</summary>
    private bool _closeupActive;

    [Header("底部面板")]
    [SerializeField, Tooltip("底部 UI 面板根节点。非战斗模式关闭，跑位完成后打开，特写期间关闭。")]
    private GameObject bottomPanelRoot;

    /// <summary>
    /// 特写期间隐藏常驻 HUD（CTB、血条、指令面板）；结束时按战斗 HUD 的启动状态恢复。
    /// 指令面板不在这里恢复：它由状态机驱动，回到玩家输入时会自动重新显示。
    /// </summary>
    public void SetCloseupActive(bool active)
    {
        if (_closeupActive == active)
            return;

        _closeupActive = active;
        SetBattleUIVisible(!active && battleUIStarted);

        if (!active && battleUIStarted)
            RefreshActingHighlight();

        if (active && BattleCommandUI.Instance != null)
            BattleCommandUI.Instance.ClosePanel();
    }

    private void SetBattleUIVisible(bool visible)
    {
        // 特写期间强制隐藏，任何事件都不能把它重新显示出来。
        if (_closeupActive && visible)
            visible = false;

        ResolveHealthBarCanvasGroup();

        if (healthBarFadeCoroutine != null)
        {
            StopCoroutine(healthBarFadeCoroutine);
            healthBarFadeCoroutine = null;
        }

        if (healthBarPanelRoot != null && healthBarPanelRoot.activeSelf != visible)
            healthBarPanelRoot.SetActive(visible);

        // CTB 不能用 SetActive 隐藏：组件被禁用会丢失当前行动者与图标状态，
        // 重新显示时大头像为空，下一位行动者会“移入空头像”而不是替换。
        // 改用 CanvasGroup 只隐藏显示，状态与动画继续保留。
        ResolveCtbCanvasGroup();
        if (ctbCanvasGroup != null)
        {
            ctbCanvasGroup.alpha = visible ? 1f : 0f;
            ctbCanvasGroup.interactable = visible;
            ctbCanvasGroup.blocksRaycasts = visible;
        }

        if (healthBarCanvasGroup != null)
        {
            healthBarCanvasGroup.alpha = visible ? 1f : 0f;
            healthBarCanvasGroup.interactable = visible;
            healthBarCanvasGroup.blocksRaycasts = visible;
        }

        // 底部面板（当前行动者立绘等）同样用 CanvasGroup 隐藏，
        // 这样里面的动画不会因为节点被禁用而中断。
        ApplyBottomPanelVisibility(visible);
    }

    [Header("底部面板节奏")]
    [SerializeField, Min(0f), Tooltip("底部面板淡入时长。0 表示瞬时显示。")]
    private float bottomPanelFadeInDuration = 0.12f;

    [SerializeField, Min(0f), Tooltip("底部面板淡出时长。特写开始时希望它快速让位，通常比淡入短。")]
    private float bottomPanelFadeOutDuration = 0.06f;

    private Coroutine bottomPanelFadeCoroutine;

    private void ApplyBottomPanelVisibility(bool visible)
    {
        ResolveBottomPanelCanvasGroup();
        if (bottomPanelCanvasGroup == null)
            return;

        bottomPanelCanvasGroup.interactable = visible;
        bottomPanelCanvasGroup.blocksRaycasts = visible;

        if (bottomPanelFadeCoroutine != null)
        {
            StopCoroutine(bottomPanelFadeCoroutine);
            bottomPanelFadeCoroutine = null;
        }

        float targetAlpha = visible ? 1f : 0f;
        float duration = visible ? bottomPanelFadeInDuration : bottomPanelFadeOutDuration;

        if (duration <= 0f || !isActiveAndEnabled)
        {
            bottomPanelCanvasGroup.alpha = targetAlpha;
            return;
        }

        bottomPanelFadeCoroutine = StartCoroutine(FadeBottomPanel(targetAlpha, duration));
    }

    private IEnumerator FadeBottomPanel(float targetAlpha, float duration)
    {
        float startAlpha = bottomPanelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bottomPanelCanvasGroup.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        bottomPanelCanvasGroup.alpha = targetAlpha;
        bottomPanelFadeCoroutine = null;
    }

    /// <summary>底部 UI 面板根节点的 CanvasGroup。</summary>
    private CanvasGroup bottomPanelCanvasGroup;

    private void ResolveBottomPanelCanvasGroup()
    {
        if (bottomPanelRoot == null)
            return;

        if (bottomPanelCanvasGroup == null)
            bottomPanelCanvasGroup = bottomPanelRoot.GetComponent<CanvasGroup>();

        if (bottomPanelCanvasGroup == null)
            bottomPanelCanvasGroup = bottomPanelRoot.AddComponent<CanvasGroup>();
    }

    /// <summary>CTB 根节点的 CanvasGroup，用于隐藏而不销毁其状态。</summary>
    private CanvasGroup ctbCanvasGroup;

    private void ResolveCtbCanvasGroup()
    {
        if (ctbRoot == null)
            return;

        if (ctbCanvasGroup == null)
            ctbCanvasGroup = ctbRoot.GetComponent<CanvasGroup>();

        // 运行时补上，避免额外的 Inspector 配置步骤。
        if (ctbCanvasGroup == null)
            ctbCanvasGroup = ctbRoot.AddComponent<CanvasGroup>();
    }

    private void ResolveHealthBarCanvasGroup()
    {
        if (healthBarPanelRoot == null)
            return;

        if (healthBarCanvasGroup == null)
            healthBarCanvasGroup = healthBarPanelRoot.GetComponent<CanvasGroup>();

        // 运行时补上 CanvasGroup，避免额外的 Inspector 配置步骤。
        if (healthBarCanvasGroup == null)
            healthBarCanvasGroup = healthBarPanelRoot.AddComponent<CanvasGroup>();
    }

    private void StartHealthBarFadeIn()
    {
        ResolveHealthBarCanvasGroup();
        if (healthBarCanvasGroup == null)
            return;

        if (healthBarFadeCoroutine != null)
            StopCoroutine(healthBarFadeCoroutine);

        healthBarCanvasGroup.alpha = 0f;
        healthBarCanvasGroup.interactable = false;
        healthBarCanvasGroup.blocksRaycasts = false;
        healthBarFadeCoroutine = StartCoroutine(FadeHealthBarsIn());
    }

    private IEnumerator FadeHealthBarsIn()
    {
        float duration = Mathf.Max(0.01f, healthBarFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            healthBarCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        healthBarCanvasGroup.alpha = 1f;
        healthBarCanvasGroup.interactable = true;
        healthBarCanvasGroup.blocksRaycasts = true;
        healthBarFadeCoroutine = null;
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
            bar.SetActing(ally == displayedActingEntity);
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

    /// <summary>
    /// 站位变化后按角色逻辑 Position 重新排列血条，并让每个血条的视觉根节点短暂滑向新位置。
    /// 例如 1 号退到 4 号时，原 2/3/4 号向上补位，1 号向下移动。
    /// </summary>
    public void RefreshHealthBarOrderAnimated()
    {
        var oldPositions = new Dictionary<HealthBarController, Vector2>();
        foreach (HealthBarController bar in spawnedHealthBars)
        {
            if (bar != null && bar.transform is RectTransform rect)
                oldPositions[bar] = rect.anchoredPosition;
        }

        SortHealthBarsByPosition();
        Canvas.ForceUpdateCanvases();

        foreach (HealthBarController bar in spawnedHealthBars)
        {
            if (bar == null || !oldPositions.TryGetValue(bar, out Vector2 oldPosition) ||
                !(bar.transform is RectTransform rect))
                continue;

            Vector2 offset = oldPosition - rect.anchoredPosition;
            if (offset.sqrMagnitude > 0.25f)
                bar.AnimateReorderOffset(offset, 0.18f);
        }
    }

    private void RefreshActingHighlight()
    {
        foreach (HealthBarController bar in spawnedHealthBars)
        {
            if (bar == null)
                continue;

            bool isActing = displayedActingEntity != null &&
                            displayedActingEntity.IsPlayer &&
                            bar.BoundCharacter == displayedActingEntity.RuntimeData;
            bar.SetActing(isActing);
        }
    }

    private static int GetPosition(BattleEntity entity)
    {
        int position = entity.RuntimeData != null ? entity.RuntimeData.Position : 0;
        return position > 0 ? position : int.MaxValue;
    }
}

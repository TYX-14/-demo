using System;
using System.Collections.Generic;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 统一管理战斗中的地面光圈。
/// 本组件应放在 Persistent 场景中的常驻对象上；位置只读取 BattleFieldLayout 的槽位数据，
/// 不依赖 BattleUnit 的 Transform。
/// </summary>
public sealed class BattleVFXManager : MonoBehaviour, IEventReceiver<CTBDisplayedActorChangedEvent>, IEventReceiver<BattleEntityPositionChangedEvent>
{
    public static BattleVFXManager Instance { get; private set; }

    [Header("光圈预制体")]
    [SerializeField] private ParticleSystem redLegalTargetPrefab;
    [SerializeField] private ParticleSystem blueCurrentActorPrefab;

    [Header("缩放与位置")]
    [SerializeField, Min(0.01f), Tooltip("所有光圈的基础缩放倍率，可在 Inspector 中调整测试。")]
    private float baseScale = 0.5f;
    [SerializeField, Tooltip("可微调光圈相对槽位中心的世界坐标偏移。默认保持为零。")]
    private Vector3 worldPositionOffset = Vector3.zero;

    private readonly List<ParticleSystem> _redPool = new();
    private readonly List<ParticleSystem> _bluePool = new();
    private readonly List<ParticleSystem> _activeRedEffects = new();
    // 敌方行动意图使用独立效果池，避免与玩家手动选目标时的红圈互相清理。
    private readonly List<ParticleSystem> _enemyIntentPool = new();
    private readonly List<ParticleSystem> _activeEnemyIntentEffects = new();
    private ParticleSystem _activeBlueEffect;
    private BattleEntity _displayedActor;
    private BattleFieldLayout _layout;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("场景中存在重复的 BattleVFXManager，重复对象将被销毁。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Subscribe<BattleEntityPositionChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Unsubscribe<BattleEntityPositionChangedEvent>(this);
        _displayedActor = null;
        ClearLegalTargetHighlights();
        ClearEnemyActionTargets();
        HideBlueEffect();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>CTB 大头像完成切换后再移动蓝色光圈，与血条高亮保持同一展示时机。</summary>
    public void OnEvent(CTBDisplayedActorChangedEvent evt)
    {
        _displayedActor = evt.Entity;
        HideBlueEffect();

        if (evt.Entity == null)
            return;

        if (!TryGetLayout())
            return;

        if (!TryGetWorldPosition(evt.Entity, null, out Vector3 position, out int occupiedCount))
            return;

        _activeBlueEffect = GetEffect(_bluePool, blueCurrentActorPrefab);
        ShowEffect(_activeBlueEffect, position, occupiedCount,
            blueCurrentActorPrefab != null ? blueCurrentActorPrefab.transform.localScale : Vector3.one);
    }


    /// <summary>当前展示的行动者完成位移后，只更新蓝圈位置，不重播粒子。</summary>
    public void OnEvent(BattleEntityPositionChangedEvent evt)
    {
        if (evt.Entity == null || evt.Entity != _displayedActor || _activeBlueEffect == null)
            return;

        if (TryGetWorldPosition(evt.Entity, null, out Vector3 position, out _))
            _activeBlueEffect.transform.position = position;
    }

    /// <summary>
    /// 显示传入的合法目标红圈。目标列表由现有 TargetSelectionState / BattleTargeting 提供，
    /// 本管理器只负责把效果放到对应槽位。
    /// </summary>
    public void ShowLegalTargetHighlights(
        IReadOnlyList<BattleEntity> legalTargets,
        Func<BattleEntity, int> getLogicalPosition)
    {
        ClearLegalTargetHighlights();

        if (legalTargets == null || legalTargets.Count == 0 || !TryGetLayout())
            return;

        for (int i = 0; i < legalTargets.Count; i++)
        {
            BattleEntity entity = legalTargets[i];
            if (entity == null || !entity.IsAlive)
                continue;

            if (!TryGetWorldPosition(entity, getLogicalPosition, out Vector3 position, out int occupiedCount))
                continue;

            ParticleSystem effect = GetEffect(_redPool, redLegalTargetPrefab);
            ShowEffect(effect, position, occupiedCount,
                redLegalTargetPrefab != null ? redLegalTargetPrefab.transform.localScale : Vector3.one);
            _activeRedEffects.Add(effect);
        }
    }

    /// <summary>清除当前技能的所有红色合法目标光圈，但不影响蓝色行动者光圈。</summary>
    public void ClearLegalTargetHighlights()
    {
        for (int i = 0; i < _activeRedEffects.Count; i++)
            HideEffect(_activeRedEffects[i]);

        _activeRedEffects.Clear();
    }

    /// <summary>
    /// 显示敌方行动已经锁定的友方目标。使用独立效果池，不会清理玩家目标选择的红圈。
    /// 传入 BattleActionContext 中已经解析完成的目标，避免提示与实际结算目标不一致。
    /// </summary>
    public void ShowEnemyActionTargets(IReadOnlyList<BattleEntity> resolvedTargets)
    {
        ClearEnemyActionTargets();

        if (resolvedTargets == null || resolvedTargets.Count == 0 || !TryGetLayout())
            return;

        for (int i = 0; i < resolvedTargets.Count; i++)
        {
            BattleEntity entity = resolvedTargets[i];
            // 红色敌意提示只标记敌人本次行动实际影响的玩家单位。
            // 敌方自我强化、敌方治疗等行动不会错误地把敌方单位标成攻击目标。
            if (entity == null || !entity.IsAlive || !entity.IsPlayer)
                continue;

            if (!TryGetWorldPosition(entity, null, out Vector3 position, out int occupiedCount))
                continue;

            ParticleSystem effect = GetEffect(_enemyIntentPool, redLegalTargetPrefab);
            ShowEffect(effect, position, occupiedCount,
                redLegalTargetPrefab != null ? redLegalTargetPrefab.transform.localScale : Vector3.one);
            if (effect != null)
                _activeEnemyIntentEffects.Add(effect);
        }
    }

    /// <summary>只清理敌方行动意图红圈，不影响玩家目标选择红圈或蓝色行动者光圈。</summary>
    public void ClearEnemyActionTargets()
    {
        for (int i = 0; i < _activeEnemyIntentEffects.Count; i++)
            HideEffect(_activeEnemyIntentEffects[i]);

        _activeEnemyIntentEffects.Clear();
    }

    private bool TryGetLayout()
    {
        if (_layout != null)
            return true;

        _layout = FindAnyObjectByType<BattleFieldLayout>();
        if (_layout == null)
            Debug.LogWarning("BattleVFXManager：当前未找到 BattleFieldLayout，暂时无法定位战斗光圈。", this);

        return _layout != null;
    }

    private bool TryGetWorldPosition(
        BattleEntity entity,
        Func<BattleEntity, int> getLogicalPosition,
        out Vector3 position,
        out int occupiedCount)
    {
        position = Vector3.zero;
        occupiedCount = 1;

        if (entity == null || !TryGetLayout())
            return false;

        int logicalPosition = getLogicalPosition != null
            ? getLogicalPosition(entity)
            : (entity.RuntimeData != null ? entity.RuntimeData.Position : 0);

        if (logicalPosition < 1 || logicalPosition > 4)
        {
            Debug.LogWarning($"BattleVFXManager：单位 {entity.ID} 的逻辑站位无效（{logicalPosition}），跳过光圈。", this);
            return false;
        }

        if (entity.IsPlayer)
        {
            position = _layout.GetAllySlotPos(logicalPosition);
        }
        else
        {
            EnemyDfinitionSo enemyDefinition = entity.Definition as EnemyDfinitionSo;
            occupiedCount = enemyDefinition != null
                ? Mathf.Clamp(enemyDefinition.occupiedSlotCount, 1, 4)
                : 1;

            // BattleFieldLayout 已经提供连续占格坐标的平均值，因此大型敌人自动落在占格中心。
            position = _layout.GetEnemySlotPos(logicalPosition, occupiedCount);
        }

        position += worldPositionOffset;
        return true;
    }

    private ParticleSystem GetEffect(List<ParticleSystem> pool, ParticleSystem prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("BattleVFXManager：尚未在 Inspector 中配置所需的光圈 Prefab。", this);
            return null;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
                return pool[i];
        }

        ParticleSystem instance = Instantiate(prefab, transform);
        instance.gameObject.name = prefab.name + " (Pooled)";
        instance.gameObject.SetActive(false);
        pool.Add(instance);
        return instance;
    }

    private void ShowEffect(ParticleSystem effect, Vector3 position, int occupiedCount, Vector3 prefabScale)
    {
        if (effect == null)
            return;

        effect.transform.position = position;
        // 单槽位单位保持 Prefab 原始尺寸；多槽位单位按占格数量的平方根扩展，
        // 避免占格翻倍时光圈直径也直接翻倍，导致特效过大。
        float occupiedSlotMultiplier = Mathf.Sqrt(Mathf.Max(1, occupiedCount));
        float finalScale = baseScale * occupiedSlotMultiplier;
        effect.transform.localScale = Vector3.Scale(prefabScale, Vector3.one * finalScale);

        if (!effect.gameObject.activeSelf)
            effect.gameObject.SetActive(true);

        effect.Clear(true);
        effect.Play(true);
    }

    private void HideBlueEffect()
    {
        if (_activeBlueEffect == null)
            return;

        HideEffect(_activeBlueEffect);
        _activeBlueEffect = null;
    }

    private static void HideEffect(ParticleSystem effect)
    {
        if (effect == null)
            return;

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
    }
}

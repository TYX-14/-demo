using MFrameork.Event;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 战斗系统控制器。
/// 负责管理战斗状态的流转、实体的集合，并通过状态机模式驱动整体战斗循环的运行与停止。
/// </summary>
public class BattleController : MonoBehaviour, IEventReceiver<GameModeChangedEvent>
{
    [Header("场地管理")]
    [SerializeField] private BattleFieldManager fieldManager;
    public BattleFieldManager FieldManager => fieldManager;

    private readonly List<BattleEntity> _allEntities = new();
    public List<BattleEntity> AllEntities => _allEntities;
    public BattleEntity CurrentEntity { get; private set; }
    private BattleRoundScheduler _roundScheduler;
    public BattleActionContext PendingAction { get; private set; }
    private BattleActionContext _activeActionContext;
    public bool IsActionInProgress => _activeActionContext != null;

    /// <summary>战斗流程的当前行动者唯一写入入口，并统一发布变更事件。</summary>
    public void SetCurrentEntity(BattleEntity entity)
    {
        CurrentEntity = entity;
        EventBus.Publish(new CurrentEntityChangedEvent(entity));
    }

    /// <summary>战斗实体生成完成后初始化 CTB 调度器。</summary>
    public void InitializeRoundScheduler()
    {
        _roundScheduler = new BattleRoundScheduler();
        _roundScheduler.Initialize(_allEntities);
        // 此处只准备行动顺序，不提前向 UI 推送队列；跑位完成后选出首位行动者时再发布显示快照。
    }

    /// <summary>供行动者分发状态取得调度器选出的下一位实体。</summary>
    public BattleEntity TakeNextScheduledEntity()
    {
        if (_roundScheduler == null)
            InitializeRoundScheduler();

        return _roundScheduler != null ? _roundScheduler.TakeNextEntity() : null;
    }

    /// <summary>把调度器的两个队列转换为 UI 节点并发布，不让 UI 自行排序。</summary>
    public void PublishCTBQueueUpdate()
    {
        if (_roundScheduler == null)
            return;

        EventBus.Publish(new CTBQueueUpdatedEvent(
            _roundScheduler.GetCurrentRoundDisplayNodes(),
            _roundScheduler.GetNextRoundPredictionNodes()));
    }

    /// <summary>从技能 UI 发起行动。目标选择方式由技能数据决定。</summary>
    public bool BeginSkillTargetSelection(SkillDefinitionSO skill)
    {
        bool canSelectSkill = CurrentState is PlayerInputState ||
                              CurrentState is TargetSelectionState;
        if (!_battleRunning || !canSelectSkill ||
            CurrentEntity == null || !CurrentEntity.IsPlayer || skill == null)
            return false;

        int actorPosition = GetLogicalPosition(CurrentEntity);
        if (!BattleTargeting.CanUseFromPosition(skill, actorPosition))
        {
            Debug.LogWarning(
                $"当前角色站位 {actorPosition} 不允许使用技能「{skill.SkillName}」。", this);
            CancelTargetSelectionAfterRejectedSkill();
            return false;
        }

        BattleActionContext actionContext = new(CurrentEntity, skill);
        PendingAction = actionContext;

        if (BattleTargeting.RequiresManualSelection(skill))
        {
            SetState(new TargetSelectionState(this, actionContext));
            return true;
        }

        if (!BattleTargeting.TryResolveAutomaticTargets(
                skill, CurrentEntity, _allEntities, GetLogicalPosition,
                out BattleEntity selectedInputTarget,
                out int selectedPosition,
                out List<BattleEntity> resolvedTargets))
        {
            Debug.LogWarning($"技能「{skill.SkillName}」没有符合规则的目标。", this);
            CancelTargetSelectionAfterRejectedSkill();
            return false;
        }

        actionContext.SetResolvedTargets(selectedInputTarget, selectedPosition, resolvedTargets);
        if (BattleCommandUI.Instance != null)
            BattleCommandUI.Instance.HideSkillPanel();

        SetState(new PerformActionState(this, actionContext));
        return true;
    }

    /// <summary>
    /// 玩家在目标选择期间尝试切换到不合法技能时，立即清除旧技能的视觉提示并退出旧选择状态。
    /// </summary>
    private void CancelTargetSelectionAfterRejectedSkill()
    {
        if (CurrentState is not TargetSelectionState targetSelectionState)
            return;

        targetSelectionState.ClearTargetFeedback();
        PendingAction = null;
        SetState(new PlayerInputState(this));
    }

    /// <summary>
    /// 由 PerformActionState 在正式结算前调用。只允许当前行动者执行当前待处理的行动，
    /// 并阻止同一时刻启动第二次行动。
    /// </summary>
    public bool TryBeginAction(BattleActionContext actionContext)
    {
        if (!_battleRunning ||
            actionContext == null ||
            CurrentState is not PerformActionState ||
            !ReferenceEquals(PendingAction, actionContext) ||
            _activeActionContext != null ||
            actionContext.Actor == null ||
            actionContext.Actor != CurrentEntity ||
            !actionContext.Actor.IsAlive)
        {
            return false;
        }

        _activeActionContext = actionContext;
        return true;
    }

    /// <summary>
    /// 正式提交敌方 AI 已选定的行动。AI 负责决策和目标选择，行动结算仍统一进入 PerformActionState。
    /// </summary>
    public bool SubmitAIAction(BattleActionContext actionContext)
    {
        if (!_battleRunning ||
            CurrentState is not EnemyAIState ||
            actionContext == null ||
            actionContext.Actor == null ||
            actionContext.Actor != CurrentEntity ||
            actionContext.Actor.IsPlayer ||
            !actionContext.Actor.IsAlive ||
            _activeActionContext != null)
        {
            return false;
        }

        PendingAction = actionContext;
        SetState(new PerformActionState(this, actionContext));
        return true;
    }

    /// <summary>
    /// 行动未能启动时，放弃本次待处理数据并回到适当状态。
    /// 玩家可以重新选择；敌方则跳过本次行动，避免 AI 状态卡住。
    /// </summary>
    public void RejectUnstartedAction(BattleActionContext actionContext)
    {
        if (_activeActionContext != null)
            return;

        if (ReferenceEquals(PendingAction, actionContext))
            PendingAction = null;

        if (!_battleRunning)
            return;

        if (CurrentEntity != null && CurrentEntity.IsPlayer)
            SetState(new PlayerInputState(this));
        else
            SetState(new SetNextEntityState(this));
    }

    /// <summary>
    /// 统一结束已启动的行动。成功结算后清除上下文并交还给 CTB 调度器，
    /// 由 SetNextEntityState 决定胜负或选择下一位行动者。
    /// </summary>
    public bool CompleteAction(BattleActionContext actionContext)
    {
        if (actionContext == null || !ReferenceEquals(_activeActionContext, actionContext))
        {
            Debug.LogWarning("BattleController：尝试结束的行动不是当前正在执行的行动。", this);
            return false;
        }

        _activeActionContext = null;
        if (ReferenceEquals(PendingAction, actionContext))
            PendingAction = null;

        if (_battleRunning)
            SetState(new SetNextEntityState(this));

        return true;
    }

    /// <summary>取消尚未开始结算的行动，例如玩家退出目标选择。</summary>
    public void CancelPendingAction()
    {
        if (_activeActionContext != null)
        {
            Debug.LogWarning("BattleController：行动执行中不能通过 CancelPendingAction 清除上下文。", this);
            return;
        }

        PendingAction = null;
    }

    /// <summary>敌方没有可用技能时跳过行动，仍由统一的下一行动者状态推进 CTB。</summary>
    public void SkipCurrentTurn(BattleEntity actor)
    {
        if (!_battleRunning || actor == null || actor != CurrentEntity ||
            actor.IsPlayer || _activeActionContext != null)
            return;

        Debug.LogWarning($"敌方「{actor.Definition.Name}」没有可执行的技能，本次跳过行动。", this);
        PendingAction = null;
        SetState(new SetNextEntityState(this));
    }

    /// <summary>
    /// 返回实体的逻辑站位。友方优先使用固定 Position。
    /// 敌方必须由 EnemySpawnManager 预先分配位置；若缺失则返回 0，绝不根据列表顺序猜测站位。
    /// </summary>
    public int GetLogicalPosition(BattleEntity entity)
    {
        if (entity == null)
            return 0;

        if (entity.RuntimeData != null &&
            entity.RuntimeData.Position >= 1 &&
            entity.RuntimeData.Position <= 4)
            return entity.RuntimeData.Position;

        if (entity.IsPlayer)
        {
            int allyPosition = 0;
            foreach (BattleEntity current in _allEntities)
            {
                if (current == null || !current.IsPlayer)
                    continue;

                allyPosition++;
                if (current == entity)
                    return allyPosition;
            }

            return 0;
        }

        return 0;
    }

    private BattleState _currentState;
    public BattleState CurrentState => _currentState;
    private bool _battleRunning;
    public bool IsBattleRunning => _battleRunning;
    private Coroutine _battleLoopRoutine;

    private void OnEnable()
    {
        EventBus.Subscribe(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe(this);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == GameMode.Battle)
        {
            StartBattleIfReady();
            return;
        }

        if (_battleRunning)
            StopBattle();
    }

    public void SetState(BattleState nextState) => _currentState = nextState;

    private void StartBattleIfReady()
    {
        if (_battleRunning) return;
        if (GameModeManager.Instance.CurrentGameMode != GameMode.Battle) return;
        if (!BattleService.Instance.HasPendingPayload) return;

        var payload = BattleService.Instance.ConsumeStartPayload();
        SetState(new BattleSetupState(this, payload));
        _battleRunning = true;
        _battleLoopRoutine = StartCoroutine(BattleLoopRoutine());
    }

    private IEnumerator BattleLoopRoutine()
    {
        while (_battleRunning && _currentState != null)
        {
            var stateSnapshot = _currentState;
            yield return StartCoroutine(stateSnapshot.Enter());

            if (stateSnapshot != _currentState)
            {
                yield return StartCoroutine(stateSnapshot.Exit());
                continue;
            }

            yield return StartCoroutine(stateSnapshot.Execute());
            yield return StartCoroutine(stateSnapshot.Exit());
        }

        _battleLoopRoutine = null;
    }

    public void StopBattle()
    {
        _battleRunning = false;
        _currentState = null;
        SetCurrentEntity(null);
        PendingAction = null;
        _activeActionContext = null;
        _roundScheduler = null;

        if (_battleLoopRoutine != null)
        {
            StopCoroutine(_battleLoopRoutine);
            _battleLoopRoutine = null;
        }
    }
}

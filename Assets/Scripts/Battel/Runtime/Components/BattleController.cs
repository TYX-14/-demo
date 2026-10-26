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
    public BattleEntity CurrentEntity { get; set; }
    public BattleActionContext PendingAction { get; private set; }

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
            PendingAction = null;
            return false;
        }

        actionContext.SetResolvedTargets(selectedInputTarget, selectedPosition, resolvedTargets);
        if (BattleCommandUI.Instance != null)
            BattleCommandUI.Instance.HideSkillPanel();

        SetState(new PerformActionState(this, actionContext));
        return true;
    }

    public void CancelPendingAction()
    {
        PendingAction = null;
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

        if (_battleLoopRoutine != null)
        {
            StopCoroutine(_battleLoopRoutine);
            _battleLoopRoutine = null;
        }
    }
}

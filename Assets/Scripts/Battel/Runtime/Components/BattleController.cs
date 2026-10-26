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
    /// <summary>
    /// 战场管理器引用（通过 Inspector 序列化赋值）。
    /// </summary>
    [SerializeField] private BattleFieldManager fieldManager;

    /// <summary>
    /// 外部获取战场管理器的公共只读属性。
    /// </summary>
    public BattleFieldManager FieldManager => fieldManager;

    /// <summary>
    /// 存放当前战斗中所有实体的列表集合。
    /// </summary>
    private readonly List<BattleEntity> _allEntities = new();

    /// <summary>
    /// 外部获取所有战斗实体的公共只读属性。
    /// </summary>
    public List<BattleEntity> AllEntities => _allEntities;

    /// <summary>
    /// 当前正在行动或处理的战斗实体。
    /// </summary>
    public BattleEntity CurrentEntity { get; set; }

    /// <summary>
    /// 当前等待确认或执行的行动数据。状态切换时共享同一份上下文。
    /// </summary>
    public BattleActionContext PendingAction { get; private set; }

    /// <summary>
    /// 从技能 UI 发起目标选择。只有当前玩家行动者且站位满足技能要求时才接受。
    /// </summary>
    public bool BeginSkillTargetSelection(SkillDefinitionSO skill)
    {
        // 允许在首次选择技能或已有目标选择时再次点击技能。
        // 后一种情况会用新技能上下文替换旧上下文，并重新进入目标选择状态。
        bool canSelectSkill = CurrentState is PlayerInputState ||
                              CurrentState is TargetSelectionState;
        if (!_battleRunning || !canSelectSkill ||
            CurrentEntity == null || !CurrentEntity.IsPlayer || skill == null)
            return false;

        int actorPosition = GetLogicalPosition(CurrentEntity);
        if (!ContainsPosition(skill.UsablePositions, actorPosition))
        {
            Debug.LogWarning(
                $"当前角色站位 {actorPosition} 不允许使用技能「{skill.SkillName}」。", this);
            return false;
        }

        PendingAction = new BattleActionContext(CurrentEntity, skill);
        SetState(new TargetSelectionState(this, PendingAction));
        return true;
    }

    /// <summary>
    /// 取消尚未确认的行动数据。取消目标选择不会消耗回合或资源。
    /// </summary>
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

    private static bool ContainsPosition(System.Collections.Generic.IReadOnlyList<int> positions, int position)
    {
        if (positions == null)
            return false;

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i] == position)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 当前战斗运行所处的状态。
    /// </summary>
    private BattleState _currentState;

    /// <summary>
    /// 外部读取当前状态，用于让等待玩家输入或 AI 行为的状态感知状态切换并结束执行协程。
    /// </summary>
    public BattleState CurrentState => _currentState;

    /// <summary>
    /// 标记战斗循环是否正在运行的后台布尔值。
    /// </summary>
    private bool _battleRunning;

    /// <summary>
    /// 外部判断战斗是否正在运行的公共只读属性。
    /// </summary>
    public bool IsBattleRunning => _battleRunning;

    /// <summary>
    /// 战斗主循环的协程引用。
    /// </summary>
    private Coroutine _battleLoopRoutine;

    /// <summary>
    /// 脚本启用时，向事件总线订阅当前对象的事件接收。
    /// </summary>
    private void OnEnable()
    {
        EventBus.Subscribe(this);
    }

    /// <summary>
    /// 脚本禁用时，从事件总线取消订阅当前对象的事件接收。
    /// </summary>
    private void OnDisable()
    {
        EventBus.Unsubscribe(this);
    }

    #region 事件监听

    /// <summary>
    /// 接收并处理游戏模式切换事件的回调方法。
    /// </summary>
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == GameMode.Battle)
        {
            StartBattleIfReady();
            return;
        }

        // 停止战斗
        if (_battleRunning)
            StopBattle();
    }

    #endregion

    /// <summary>
    /// 设置当前战斗状态。
    /// </summary>
    /// <param name="nextState">要切换到的下一个状态。</param>
    public void SetState(BattleState nextState) => _currentState = nextState;

    /// <summary>
    /// 当满足条件时（战斗未运行、处于战斗模式且有数据载荷），准备并启动战斗。
    /// </summary>
    private void StartBattleIfReady()
    {
        if (_battleRunning) return;

        if (GameModeManager.Instance.CurrentGameMode != GameMode.Battle) return;

        if (!BattleService.Instance.HasPendingPayload) return;

        var payload = BattleService.Instance.ConsumeStartPayload();
        // 进入战斗第一个阶段
        SetState(new BattleSetupState(this, payload));

        _battleRunning = true;
        _battleLoopRoutine = StartCoroutine(BattleLoopRoutine());
    }

    /// <summary>
    /// 标准状态机战斗主循环协程。
    /// <para>循环执行逻辑：(Enter 资源准备 -> Execute 行为触发 -> Exit 资源回收)</para>
    /// </summary>
    private IEnumerator BattleLoopRoutine()
    {
        while (_battleRunning && _currentState != null)
        {
            // 先记住这一轮开始时的状态，避免 Enter 里切状态后还继续跑错流程。
            var stateSnapshot = _currentState;

            yield return StartCoroutine(stateSnapshot.Enter());

            // 如果 Enter 期间已经切到别的状态了，这一轮就不要继续往下执行。
            if (stateSnapshot != _currentState)
            {
                yield return StartCoroutine(stateSnapshot.Exit());
                continue;
            }

            // 当前状态真正执行业务。
            yield return StartCoroutine(stateSnapshot.Execute());

            // 当前状态自己的收尾清理。
            yield return StartCoroutine(stateSnapshot.Exit());
        }

        _battleLoopRoutine = null;
    }

    /// <summary>
    /// 停止当前的战斗，清理状态并中断协程。
    /// </summary>
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

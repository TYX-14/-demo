using System.Collections;
using UnityEngine;

/// <summary>
/// 下一行动者选择状态，也就是战斗状态机中的回合分发器。
///
/// 主要职责：
/// 1. 检查战斗是否已经结束：任意一方没有存活实体时停止当前战斗循环。
/// 2. 从 BattleRoundScheduler 取得下一位真正要行动的实体。
/// 3. 通过 BattleController 的统一入口更新当前行动者，并发布 CurrentEntityChangedEvent，
///    供当前行动者 UI 与时间轴显示订阅同步。
/// 4. 根据行动者阵营切换到玩家输入状态或敌方 AI 状态。
/// </summary>
public class SetNextEntityState : BattleState
{
    /// <summary>
    /// 初始化下一行动者选择状态。
    /// </summary>
    /// <param name="controller">管理当前战斗的控制器。</param>
    public SetNextEntityState(BattleController controller) : base(controller)
    {
    }

    /// <summary>
    /// 检查战斗结束条件，选择下一位行动者并分发到对应阵营状态。
    /// </summary>
    public override IEnumerator Execute()
    {
        if (IsBattleFinished())
        {
            Debug.Log("战斗结束：至少一方已没有存活的战斗实体。");
            _controller.StopBattle();
            yield break;
        }

        // 上一行动完整结束后才进入这里；队列耗尽时先明确推进回合，再选择行动者。
        _controller.TryAdvanceRound();

        BattleEntity nextEntity = _controller.TakeNextScheduledEntity();
        if (nextEntity == null)
        {
            Debug.LogWarning("SetNextEntityState：没有找到可行动的存活实体，停止战斗以避免状态机空转。");
            _controller.StopBattle();
            yield break;
        }

        // 输出选人结果，便于在 Unity Console 中确认当前行动者、阵营、站位和速度。
        string characterName = nextEntity.RuntimeData != null &&
                               !string.IsNullOrEmpty(nextEntity.RuntimeData.DisplayName)
            ? nextEntity.RuntimeData.DisplayName
            : (nextEntity.Definition != null && !string.IsNullOrEmpty(nextEntity.Definition.Name)
                ? nextEntity.Definition.Name
                : nextEntity.ID);
        int entityIndex = _controller.AllEntities.IndexOf(nextEntity);
        int displayedPosition = GetPositionPriority(nextEntity, entityIndex);
        string factionName = nextEntity.IsPlayer ? "友军" : "敌军";
        Debug.Log($"[战斗行动] 当前选中：{characterName} | 阵营：{factionName} | 站位：第 {displayedPosition} 位 | 速度：{nextEntity.TotalStats.Speed}", nextEntity.Unit);

        // 更新当前行动者，并通知 UI / 时间轴同步显示。
        _controller.SetCurrentEntity(nextEntity);
        _controller.PublishCTBQueueUpdate();

        // 眩晕在目标下一次获得行动机会时消耗一次行动次数，并直接跳过本次行动。
        if (nextEntity.RuntimeData != null && nextEntity.RuntimeData.StunActionsRemaining > 0)
        {
            nextEntity.RuntimeData.StunActionsRemaining--;
            nextEntity.Unit?.RefreshStunVisual();
            Debug.Log($"[眩晕] {characterName} 被眩晕影响，跳过本次行动；剩余次数：{nextEntity.RuntimeData.StunActionsRemaining}。", nextEntity.Unit);
            _controller.SetState(new SetNextEntityState(_controller));
            yield break;
        }

        // 根据阵营把状态机控制权交给玩家输入或敌方 AI。
        if (nextEntity.IsPlayer)
        {
            _controller.SetState(new PlayerInputState(_controller));
        }
        else
        {
            _controller.SetState(new EnemyAIState(_controller));
        }

        yield break;
    }

    /// <summary>
    /// 当前演示版的战斗结束判断。
    /// 只要玩家或敌方任意一方没有存活实体，就认为战斗结束。
    /// 后续可替换为正式的胜负结算服务。
    /// </summary>
    private bool IsBattleFinished()
    {
        bool hasLivingPlayer = false;
        bool hasLivingEnemy = false;

        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || !entity.IsAlive)
                continue;

            if (entity.IsPlayer)
                hasLivingPlayer = true;
            else
                hasLivingEnemy = true;

            if (hasLivingPlayer && hasLivingEnemy)
                return false;
        }

        return !hasLivingPlayer || !hasLivingEnemy;
    }

    /// <summary>
    /// 返回用于调试日志的显示站位；没有有效 Position 时，使用同阵营列表顺序。
    /// </summary>
    /// <summary>
    /// 返回用于调试日志的显示站位。
    /// Position 大于 0 时优先使用角色数据中的固定站位；
    /// 否则使用该实体在同阵营实体列表中的先后顺序。
    /// </summary>
    private int GetPositionPriority(BattleEntity entity, int entityIndex)
    {
        if (entity.RuntimeData != null && entity.RuntimeData.Position > 0)
            return entity.RuntimeData.Position;

        int sideOrder = 1;
        for (int i = 0; i < entityIndex; i++)
        {
            BattleEntity previous = _controller.AllEntities[i];
            if (previous != null && previous.IsPlayer == entity.IsPlayer)
                sideOrder++;
        }

        return sideOrder;
    }
}

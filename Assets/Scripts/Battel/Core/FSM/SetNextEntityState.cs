using System.Collections;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 下一行动者选择状态，也就是战斗状态机中的回合分发器。
///
/// 主要职责：
/// 1. 检查战斗是否已经结束：任意一方没有存活实体时停止当前战斗循环。
/// 2. 从当前战斗实体中选出下一位真正要行动的实体。
///    当前演示阶段暂用速度从高到低排序；速度相同时，站位靠前者优先。
///    后续接入 BattleRoundScheduler 后，应由调度器提供下一位行动者。
/// 3. 更新 BattleController.CurrentEntity，并发布 CurrentEntityChangedEvent，
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

        BattleEntity nextEntity = FindNextEntity();
        if (nextEntity == null)
        {
            Debug.LogWarning("SetNextEntityState：没有找到可行动的存活实体，停止战斗以避免状态机空转。");
            _controller.StopBattle();
            yield break;
        }

        // 更新当前行动者，并通知 UI / 时间轴同步显示。
        _controller.CurrentEntity = nextEntity;
        EventBus.Publish(new CurrentEntityChangedEvent(nextEntity));

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
    /// 演示阶段的临时选人算法：
    /// 先选速度最高者；速度相同则选择站位数字更小、也就是更靠前的实体。
    /// 当角色没有有效 Position 时，使用其在同阵营列表中的顺序作为备用站位。
    /// </summary>
    private BattleEntity FindNextEntity()
    {
        BattleEntity bestEntity = null;
        int bestSpeed = int.MinValue;
        int bestPosition = int.MaxValue;

        for (int i = 0; i < _controller.AllEntities.Count; i++)
        {
            BattleEntity candidate = _controller.AllEntities[i];
            if (candidate == null || !candidate.IsAlive)
                continue;

            int speed = candidate.TotalStats.Speed;
            int position = GetPositionPriority(candidate, i);

            if (bestEntity == null ||
                speed > bestSpeed ||
                (speed == bestSpeed && position < bestPosition))
            {
                bestEntity = candidate;
                bestSpeed = speed;
                bestPosition = position;
            }
        }

        return bestEntity;
    }

    /// <summary>
    /// 返回用于同速排序的站位优先级。
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

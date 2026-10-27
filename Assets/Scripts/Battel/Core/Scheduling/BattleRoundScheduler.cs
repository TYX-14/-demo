using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CTB 调度器。维护当前行动轮剩余队列与下一轮预测。
/// 目前以“行动时间 = 100 / 速度”为基础时间模型；后续速度变化、额外行动、延迟/加速可在此集中扩展。
/// 此类不依赖 Unity 场景或 UI。
/// </summary>
public sealed class BattleRoundScheduler
{
    private const float BaseActionDelay = 100f;

    private readonly List<BattleEntity> _entities = new();
    private readonly List<BattleEntity> _currentRoundQueue = new();
    private readonly Dictionary<BattleEntity, float> _nextActionTime = new();
    private readonly HashSet<BattleEntity> _actedThisRound = new();
    private readonly Dictionary<BattleEntity, int> _stableOrder = new();

    public IReadOnlyList<BattleEntity> CurrentRoundQueue => _currentRoundQueue;

    public void Initialize(IEnumerable<BattleEntity> entities)
    {
        _entities.Clear();
        _currentRoundQueue.Clear();
        _nextActionTime.Clear();
        _actedThisRound.Clear();
        _stableOrder.Clear();

        if (entities == null)
            return;

        foreach (BattleEntity entity in entities)
        {
            if (entity == null || !entity.IsAlive || _nextActionTime.ContainsKey(entity))
                continue;

            _stableOrder[entity] = _entities.Count;
            _entities.Add(entity);
            _nextActionTime[entity] = 0f;
        }

        BuildCurrentRoundQueue();
    }

    /// <summary>取得并消费下一位行动者，同时为其记录下一次行动时间。</summary>
    public BattleEntity TakeNextEntity()
    {
        RemoveInvalidEntities();
        if (_currentRoundQueue.Count == 0)
        {
            _actedThisRound.Clear();
            BuildCurrentRoundQueue();
        }

        if (_currentRoundQueue.Count == 0)
            return null;

        BattleEntity next = _currentRoundQueue[0];
        _currentRoundQueue.RemoveAt(0);
        _actedThisRound.Add(next);
        _nextActionTime[next] += GetActionDelay(next);
        return next;
    }

    /// <summary>返回当前行动轮中尚未行动的实体节点，顺序可直接用于 UI。</summary>
    public CTBDisplayNode[] GetCurrentRoundDisplayNodes()
    {
        RemoveInvalidEntities();
        return CTBDisplayNode.FromEntities(_currentRoundQueue);
    }

    /// <summary>
    /// 预测下一行动轮顺序。未完成本轮行动的实体需先行动一次，再进入下一轮；
    /// 已完成本轮行动的实体则从其已推进的下一行动时间继续预测。
    /// </summary>
    public CTBDisplayNode[] GetNextRoundPredictionNodes()
    {
        RemoveInvalidEntities();

        var prediction = _entities
            .Where(entity => entity != null && entity.IsAlive)
            .OrderBy(GetPredictedNextRoundTime)
            .ThenBy(GetPositionPriority)
            .ThenBy(entity => _stableOrder[entity])
            .ToList();

        return CTBDisplayNode.FromEntities(prediction);
    }

    private float GetPredictedNextRoundTime(BattleEntity entity)
    {
        float currentTime = _nextActionTime.TryGetValue(entity, out float time) ? time : 0f;
        int remainingActionsBeforeNextRound = _actedThisRound.Contains(entity) ? 1 : 2;
        return currentTime + GetActionDelay(entity) * remainingActionsBeforeNextRound;
    }

    private void BuildCurrentRoundQueue()
    {
        RemoveInvalidEntities();
        _currentRoundQueue.Clear();

        _currentRoundQueue.AddRange(_entities
            .Where(entity => entity != null && entity.IsAlive)
            .OrderBy(entity => _nextActionTime.TryGetValue(entity, out float time) ? time + GetActionDelay(entity) : float.MaxValue)
            .ThenBy(GetPositionPriority)
            .ThenBy(entity => _stableOrder[entity]));
    }

    private void RemoveInvalidEntities()
    {
        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            BattleEntity entity = _entities[i];
            if (entity != null && entity.IsAlive)
                continue;

            _entities.RemoveAt(i);
            _currentRoundQueue.Remove(entity);
            _nextActionTime.Remove(entity);
            _actedThisRound.Remove(entity);
            _stableOrder.Remove(entity);
        }
    }

    private static float GetActionDelay(BattleEntity entity)
    {
        return BaseActionDelay / Math.Max(1, entity != null ? entity.TotalStats.Speed : 1);
    }

    private static int GetPositionPriority(BattleEntity entity)
    {
        return entity != null && entity.RuntimeData != null && entity.RuntimeData.Position > 0
            ? entity.RuntimeData.Position
            : int.MaxValue;
    }
}

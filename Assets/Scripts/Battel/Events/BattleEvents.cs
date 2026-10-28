using MFrameork.Event;

/// <summary>
/// 战斗开始通知事件。
/// 当前只用于通知订阅者：友军完成入场跑位，战斗 HUD 可以显示。
/// 后续可按需扩展战斗类型（普通战、Boss 战等）、BGM 切换等信息。
/// </summary>
public readonly struct BattleStartedEvent : IEvent
{
}

/// <summary>调度器确认进入新一轮时发布。</summary>
public readonly struct BattleRoundChangedEvent : IEvent
{
    public int RoundNumber { get; }

    public BattleRoundChangedEvent(int roundNumber)
    {
        RoundNumber = roundNumber;
    }
}

/// <summary>
/// 当前行动者发生变化时发布的事件。
/// UI 和时间轴可以订阅该事件，同步当前行动者的高亮、头像或行动顺序显示。
/// </summary>
public readonly struct CurrentEntityChangedEvent : IEvent
{
    public BattleEntity Entity { get; }

    public CurrentEntityChangedEvent(BattleEntity entity)
    {
        Entity = entity;
    }
}


/// <summary>
/// CTB 大头像完成切换并稳定显示后发布。
/// Health Bar 等依赖视觉时机的 UI 应监听此事件，而不是提前监听 CurrentEntityChangedEvent。
/// </summary>
public readonly struct CTBDisplayedActorChangedEvent : IEvent
{
    public BattleEntity Entity { get; }

    public CTBDisplayedActorChangedEvent(BattleEntity entity)
    {
        Entity = entity;
    }
}

/// <summary>
/// CTB 两个行动池更新事件。数据已由调度器转换为 UI 可直接使用的节点。
/// </summary>
public readonly struct CTBQueueUpdatedEvent : IEvent
{
    public CTBDisplayNode[] CurrentRoundNodes { get; }
    public CTBDisplayNode[] NextRoundNodes { get; }

    public CTBQueueUpdatedEvent(CTBDisplayNode[] currentRoundNodes, CTBDisplayNode[] nextRoundNodes)
    {
        CurrentRoundNodes = currentRoundNodes ?? System.Array.Empty<CTBDisplayNode>();
        NextRoundNodes = nextRoundNodes ?? System.Array.Empty<CTBDisplayNode>();
    }
}

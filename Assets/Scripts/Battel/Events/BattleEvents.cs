using MFrameork.Event;

/// <summary>
/// 战斗开始通知事件。
/// 当前只用于通知订阅者：友军完成入场跑位，战斗 HUD 可以显示。
/// 后续可按需扩展战斗类型（普通战、Boss 战等）、BGM 切换等信息。
/// </summary>
public readonly struct BattleStartedEvent : IEvent
{
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

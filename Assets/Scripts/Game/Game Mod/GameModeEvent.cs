using MFrameork.Event;

public readonly struct GameModeChangedEvent : IEvent
{
    public readonly GameMode newMode;

    public GameModeChangedEvent(GameMode newMode)
    {
        this.newMode = newMode;
    }
}
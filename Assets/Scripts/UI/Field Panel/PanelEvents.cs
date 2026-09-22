using MFrameork.Event;

public readonly struct PanelRequestEvent : IEvent
{
    public readonly ActionBase actionBase;

    /// <summary>
    /// 这是这个事件的构造方法，他将传入的ActionBase参数赋值到事件的actionBase上
    /// </summary>
    /// <param name="actionBase"></param>
    public PanelRequestEvent(ActionBase actionBase)
    {
        this.actionBase = actionBase;
    }
}
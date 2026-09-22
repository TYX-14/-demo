using MFrameork.Event;

/// <summary>
/// 可交互状态转换事件(判断是否打开一级菜单)
/// </summary>
public readonly struct InteractionChangeEvent : IEvent//可交互状态转换事件(判断是否打开一级菜单)
{
    public readonly InteractionBase target;//可交互npc

    public readonly bool inRange;//是否在可交互范围内
    public InteractionChangeEvent(InteractionBase target,bool inRange)
    {
        this.target = target;
        this.inRange = inRange;
    }

}
/// <summary>
/// 请求交互菜单事件
/// </summary>
public readonly struct InteractionMequestEvent : IEvent//请求交互菜单事件
{
    public readonly InteractionBase target;
    public InteractionMequestEvent(InteractionBase target)
    {
        this.target = target;
        
    }
}
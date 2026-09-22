

/// <summary>
/// 战斗状态的抽象基类。
/// 定义了所有具体战斗状态（如：准备阶段、玩家回合、敌人回合等）必须遵循的规范，
/// 包含了状态生命周期中的三个核心方法：进入(Enter)、执行(Execute)、退出(Exit)。
/// </summary>
public abstract class BattleState
{
    /// <summary>
    /// 持有当前状态所属的 <see cref="BattleController"/> 引用。
    /// 供子类在状态流转时，访问战斗控制器中的公共数据或调用切换状态的方法。
    /// </summary>
    protected readonly BattleController _controller;

    /// <summary>
    /// 初始化 <see cref="BattleState"/> 基类的新实例。
    /// </summary>
    /// <param name="controller">管理此状态的战斗控制器实例。</param>
    public BattleState(BattleController controller)
    {
        _controller = controller;
    }

    /// <summary>
    /// 进入当前状态时执行的逻辑（协程）。
    /// 通常用于进行状态前的准备工作，如：锁定玩家移动、播放入场动画或分配资源。
    /// 默认为空实现，子类可根据需要选择性地覆盖此方法。
    /// </summary>
    public virtual IEnumerator Enter()
    {
        yield break; // 立即结束此协程，不做任何操作
    }

    /// <summary>
    /// 当前状态的核心执行逻辑（抽象协程）。
    /// 必须由子类（具体状态）强制实现。
    /// 负责执行此状态下的主要业务，例如等待玩家输入、执行AI逻辑、计算伤害等。
    /// </summary>
    public abstract IEnumerator Execute();

    /// <summary>
    /// 退出当前状态时执行的逻辑（协程）。
    /// 通常用于状态的清理与资源回收，如：解除移动锁定、恢复UI、重置变量等。
    /// 默认为空实现，子类可根据需要选择性地覆盖此方法。
    /// </summary>
    public virtual IEnumerator Exit()
    {
        yield break; // 立即结束此协程，不做任何操作
    }
}
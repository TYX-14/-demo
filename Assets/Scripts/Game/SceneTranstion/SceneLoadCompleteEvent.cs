using MFrameork.Event;

using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载完成事件数据类。
/// 用于在场景切换流程中，通知外部模块场景已加载完毕并附带后续模式。
/// </summary>
public readonly struct SceneLoadCompleteEvent : IEvent
{
    /// <summary>
    /// 已经加载完成的场景对象。
    /// </summary>
    public readonly Scene LaodedScene;

    /// <summary>
    /// 场景加载完成后，游戏即将进入的游戏模式。
    /// </summary>
    public readonly GameMode ModeAfterLoad;

    /// <summary>
    /// 初始化场景加载完成事件。
    /// </summary>
    /// <param name="scene">已加载的场景。</param>
    /// <param name="mode">加载后的目标游戏模式。</param>
    public SceneLoadCompleteEvent(Scene scene, GameMode mode)
    {
        LaodedScene = scene;
        ModeAfterLoad = mode;
    }
}
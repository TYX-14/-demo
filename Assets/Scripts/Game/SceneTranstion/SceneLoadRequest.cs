
using UnityEngine.AddressableAssets;

/// <summary>
/// 场景加载请求数据结构，用于封装一次场景切换所需的全部参数
/// </summary>
public struct SceneLoadRequest
{
    /// <summary>目标场景资源引用</summary>
    public readonly AssetReference Scene;

    /// <summary>淡入淡出效果类型</summary>
    public readonly FadeStyle FadeStyle;

    /// <summary>场景加载完成后的游戏模式</summary>
    public readonly GameMode ModeAfterLoad;

    /// <summary>出生点 ID，用于角色在新场景中的生成位置</summary>
    public readonly string SpawnPointId;

    /// <summary>淡出时间覆盖值（若为默认则使用系统配置）</summary>
    public readonly float FadeOutDurationOverride;

    /// <summary>淡入时间覆盖值（若为默认则使用系统配置）</summary>
    public readonly float FadeInDurationOverride;

    /// <summary>
    /// 创建一个场景加载请求
    /// </summary>
    /// <param name="scene">目标场景资源</param>
    /// <param name="fadeStyle">淡入淡出效果类型</param>
    /// <param name="modeAfterLoad">加载完成后的游戏模式</param>
    /// <param name="spawnPointId">出生点ID</param>
    /// <param name="fadeOutDurationOverride">淡出时间覆盖值</param>
    /// <param name="fadeInDurationOverride">淡入时间覆盖值</param>
    public SceneLoadRequest(
        AssetReference scene,
        FadeStyle fadeStyle,
        GameMode modeAfterLoad,
        string spawnPointId = null,
        float fadeOutDurationOverride = -1,
        float fadeInDurationOverride = -1)
    {
        Scene = scene;
        FadeStyle = fadeStyle;
        ModeAfterLoad = modeAfterLoad;
        SpawnPointId = spawnPointId;
        FadeOutDurationOverride = fadeOutDurationOverride;
        FadeInDurationOverride = fadeInDurationOverride;
    }
}
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// 场景加载管理器：负责游戏场景的加载、卸载、过渡动画以及模式切换
/// </summary>
public class SceneLoadManager : singleton<SceneLoadManager>
{
    /// <summary>是否正在加载场景，防止重复加载</summary>
    private bool isLoading;

    /// <summary>主菜单场景引用</summary>
    [SerializeField] private AssetReference menuScene;

    /// <summary>当前激活场景</summary>
    public AssetReference activeScene;

    /// <summary>黑屏停留时间（加载完成后缓冲）</summary>
    [Header("Transition Timing")]
    [SerializeField, Range(0.01f, 2f)]
    private float postLoadBlackScreenDuration = 0.35f;

    /// <summary>当前加载的场景 Handle</summary>
    private AsyncOperationHandle<SceneInstance>? currentSceneHandle;

    /// <summary>
    /// 初始化时加载当前 activeScene
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        var loadHandle =
            Addressables.LoadSceneAsync(activeScene, LoadSceneMode.Additive);

        currentSceneHandle = loadHandle;

        loadHandle.Completed += (handle) =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                return;

            SceneManager.SetActiveScene(handle.Result.Scene);
            Debug.Log(RenderSettings.sun.name);
        };
    }

    /// <summary>
    /// 外部请求加载新场景
    /// </summary>
    public void RequestLoad(SceneLoadRequest request)
    {
        if (isLoading)
            return;

        isLoading = true;
        StartCoroutine(LoadFlow(request));
    }

    /// <summary>
    /// 场景加载核心流程（包含淡入淡出 / 卸载 / 切换 / 恢复）
    /// </summary>
    private IEnumerator LoadFlow(SceneLoadRequest request)
    {
        try
        {
            // 1. 切换到交互菜单模式（锁输入）
            GameModeManager.Instance.RequestChangeMode(GameMode.Pause);

            // 2. 配置淡入淡出样式与时长
            FadeController.Instance.SetStyle(request.FadeStyle);
            FadeController.Instance.SetNextFadeDurations(
                request.FadeOutDurationOverride,
                request.FadeInDurationOverride
            );

            // 3. 等待屏幕淡出
            bool fadeOutComplete = false;
            FadeController.Instance.FadeOut(() => fadeOutComplete = true);
            yield return new WaitUntil(() => fadeOutComplete);

            // 4. 如果旧场景存在则卸载
            if (currentSceneHandle.HasValue &&
                currentSceneHandle.Value.IsValid())
            {
                yield return Addressables.UnloadSceneAsync(
                    currentSceneHandle.Value,
                    true
                );

                currentSceneHandle = null;
            }

            // 5. 加载目标场景
            var loadHandle = Addressables.LoadSceneAsync(
                request.Scene,
                LoadSceneMode.Additive
            );

            yield return loadHandle;

            bool loadSucceeded =
                loadHandle.Status == AsyncOperationStatus.Succeeded;

            if (loadSucceeded)
            {
                currentSceneHandle = loadHandle;
                activeScene = request.Scene;

               SceneManager.SetActiveScene(loadHandle.Result.Scene);

                // 是否需要恢复探索模式（提前切回）
                bool restoreExploreModeBeforeFadeIn =
                    loadSucceeded &&
                    request.ModeAfterLoad == GameMode.Explore;

                if (restoreExploreModeBeforeFadeIn)
                {
                    GameModeManager.Instance.RequestChangeMode(GameMode.Explore);
                    yield return null;
                }
            }
            else
            {
                Debug.LogError($"Failed to load scene {request.Scene}");
            }

            // 6. 黑屏缓冲时间
            yield return new WaitForSecondsRealtime(postLoadBlackScreenDuration);

            // 7. 淡入
            bool fadeInComplete = false;
            FadeController.Instance.FadeIn(() => fadeInComplete = true);
            yield return new WaitUntil(() => fadeInComplete);

            // 8. 如果不是探索模式，则在淡入后切换模式
            if (loadSucceeded &&
                request.ModeAfterLoad != GameMode.Explore)
            {
                GameModeManager.Instance.RequestChangeMode(request.ModeAfterLoad);
            }
        }
        finally
        {
            // 结束加载状态
            isLoading = false;
        }
    }
}

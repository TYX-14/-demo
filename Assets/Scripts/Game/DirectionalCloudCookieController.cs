using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 为当前激活的游戏场景主方向光应用并滚动云影 Cookie。
/// 此组件放在 Persistent 场景中，由它跟随 Addressables 场景切换。
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionalCloudCookieController : MonoBehaviour
{
    [Header("云影纹理")]
    [SerializeField] private Texture2D cloudCookie;
    [SerializeField, Min(1f)] private float cookieSize = 10f;
    [SerializeField, Min(0.01f)] private Vector2 cookieSizeMultiplier = Vector2.one;

    [Header("云影浓度")]
    [SerializeField, Range(0.1f, 4f)] private float shadowDensity = 1f;

    [Header("云影流动")]
    [SerializeField] private bool enableScrolling = true;
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.035f, 0.014f);
    [SerializeField, Min(0f)] private float scrollSpeedMultiplier = 1f;
    [SerializeField] private Vector2 initialOffset = Vector2.zero;

    private Light activeLight;
    private UniversalAdditionalLightData activeLightData;
    private Texture originalCookie;
    private Texture2D runtimeCookie;
    private Vector2 originalCookieSize;
    private Vector2 originalCookieOffset;
    private bool addedLightData;
    private Light originalSun;
    private bool changedSun;
    private int activeSceneHandle = int.MinValue;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        BindToActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        RestoreLight(true);
        activeSceneHandle = int.MinValue;
    }

    private void Update()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        // 作为事件回调之外的保险，确保激活场景变化后重新绑定。
        if (scene.handle != activeSceneHandle)
            BindToActiveScene();

        if (activeLightData != null && enableScrolling)
            activeLightData.lightCookieOffset += scrollSpeed * (scrollSpeedMultiplier * Time.deltaTime);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        // 先恢复旧场景方向光，再绑定新激活场景。
        RestoreLight(false);
        activeSceneHandle = int.MinValue;
        BindToActiveScene();
    }

    private void BindToActiveScene()
    {
        RestoreLight(false);

        Scene scene = SceneManager.GetActiveScene();
        activeSceneHandle = scene.handle;

        // Persistent 本身没有场景主光，不对它应用 Cookie。
        if (!scene.IsValid() || scene == gameObject.scene)
            return;

        if (cloudCookie == null)
        {
            Debug.LogError("[Cloud Cookie] 没有指定云影纹理。", this);
            return;
        }

        Light target = RenderSettings.sun;

        // 优先使用当前场景明确指定的 Sun。
        if (target == null ||
            target.type != LightType.Directional ||
            target.gameObject.scene != scene ||
            !target.isActiveAndEnabled)
        {
            target = null;
        }

        // 如果场景没有指定 Sun，则选当前场景中强度最高的方向光。
        if (target == null)
        {
            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light candidate in lights)
            {
                if (candidate.type != LightType.Directional ||
                    !candidate.isActiveAndEnabled ||
                    candidate.gameObject.scene != scene)
                    continue;

                if (target == null || candidate.intensity > target.intensity)
                    target = candidate;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[Cloud Cookie] 场景 {scene.name} 中没有启用的 Directional Light。", this);
            return;
        }

        // 不覆盖场景或其他系统已经使用的 Cookie。
        if (target.cookie != null && target.cookie != cloudCookie)
        {
            Debug.LogWarning($"[Cloud Cookie] {target.name} 已经使用其他 Cookie，本次跳过。", target);
            return;
        }

        activeLight = target;
        originalCookie = target.cookie;

        addedLightData = !target.TryGetComponent(out activeLightData);
        if (addedLightData)
            activeLightData = target.GetUniversalAdditionalLightData();

        originalCookieSize = activeLightData.lightCookieSize;
        originalCookieOffset = activeLightData.lightCookieOffset;

        originalSun = RenderSettings.sun;
        changedSun = originalSun != target;
        if (changedSun)
            RenderSettings.sun = target;

        runtimeCookie = CreateAdjustedCookie();
        target.cookie = runtimeCookie != null ? runtimeCookie : cloudCookie;
        activeLightData.lightCookieSize = new Vector2(
            cookieSize * cookieSizeMultiplier.x,
            cookieSize * cookieSizeMultiplier.y);
        activeLightData.lightCookieOffset = originalCookieOffset + initialOffset;

        Debug.Log($"[Cloud Cookie] 已应用到场景 {scene.name} 的方向光：{target.name}", target);
    }

    private Texture2D CreateAdjustedCookie()
    {
        // 1 是原始浓度，低于 1 会变淡，高于 1 会加深中间灰度。
        if (Mathf.Approximately(shadowDensity, 1f))
            return null;

        if (!cloudCookie.isReadable)
        {
            Debug.LogError("[Cloud Cookie] 云影纹理必须启用 Read/Write，才能调整云影浓度。", this);
            return null;
        }

        Color32[] pixels = cloudCookie.GetPixels32();
        float exponent = Mathf.Max(0.01f, shadowDensity);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            pixel.r = AdjustCookieChannel(pixel.r, exponent);
            pixel.g = AdjustCookieChannel(pixel.g, exponent);
            pixel.b = AdjustCookieChannel(pixel.b, exponent);
            pixels[i] = pixel;
        }

        Texture2D adjusted = new Texture2D(
            cloudCookie.width,
            cloudCookie.height,
            TextureFormat.RGBA32,
            cloudCookie.mipmapCount > 1,
            true)
        {
            name = cloudCookie.name + "_RuntimeAdjusted",
            wrapMode = cloudCookie.wrapMode,
            filterMode = cloudCookie.filterMode,
            anisoLevel = cloudCookie.anisoLevel
        };

        adjusted.SetPixels32(pixels);
        adjusted.Apply(true, true);
        return adjusted;
    }

    private static byte AdjustCookieChannel(byte channel, float exponent)
    {
        float normalized = channel / 255f;
        return (byte)Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Pow(normalized, exponent) * 255f),
            0,
            255);
    }

    private void RestoreLight(bool restoreSun)
    {
        if (activeLight == null)
            return;

        Light previousLight = activeLight;
        previousLight.cookie = originalCookie;

        if (runtimeCookie != null)
        {
            Destroy(runtimeCookie);
            runtimeCookie = null;
        }

        if (activeLightData != null)
        {
            activeLightData.lightCookieSize = originalCookieSize;
            activeLightData.lightCookieOffset = originalCookieOffset;

            if (addedLightData)
                Destroy(activeLightData);
        }

        if (restoreSun &&
            changedSun &&
            SceneManager.GetActiveScene().handle == activeSceneHandle)
        {
            RenderSettings.sun = originalSun;
        }

        activeLight = null;
        activeLightData = null;
        originalCookie = null;
        addedLightData = false;
        changedSun = false;
    }
}

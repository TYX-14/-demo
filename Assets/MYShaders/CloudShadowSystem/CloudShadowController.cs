using UnityEngine;

/// <summary>
/// 为全屏云影提供统一的世界空间参数。将组件挂到场景中的一个空物体即可。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CloudShadowController : MonoBehaviour
{
    [Header("总开关")]
    public bool 启用云影 = true;

    [Range(0f, 1f)]
    public float 云影强度 = 0.58f;

    [Header("云层形状")]
    [Range(0.001f, 0.1f)]
    public float 云影大小 = 0.014f;

    [Range(0f, 1f)]
    public float 云层覆盖率 = 0.52f;

    [Range(0.01f, 0.5f)]
    public float 边缘柔和度 = 0.16f;

    [Range(0f, 1f)]
    public float 细节变化 = 0.35f;

    [Header("云层移动")]
    public Vector2 流动方向 = new Vector2(1f, 0.25f);

    [Range(0f, 2f)]
    public float 流动速度 = 0.12f;

    [Header("光照投影")]
    [Min(1f)]
    public float 云层高度 = 180f;

    public bool 自动跟随场景主光源 = true;

    [Tooltip("没有设置 RenderSettings.sun 时使用。方向表示从地面指向太阳。")]
    public Vector3 手动太阳方向 = new Vector3(0.3f, 0.85f, 0.2f);

    public Color 阴影色 = new Color(0.30f, 0.34f, 0.42f, 1f);

    private static readonly int EnabledId = Shader.PropertyToID("_CloudShadowEnabled");
    private static readonly int StrengthId = Shader.PropertyToID("_CloudShadowStrength");
    private static readonly int ScaleId = Shader.PropertyToID("_CloudShadowScale");
    private static readonly int CoverageId = Shader.PropertyToID("_CloudShadowCoverage");
    private static readonly int SoftnessId = Shader.PropertyToID("_CloudShadowSoftness");
    private static readonly int DetailId = Shader.PropertyToID("_CloudShadowDetail");
    private static readonly int SpeedId = Shader.PropertyToID("_CloudShadowSpeed");
    private static readonly int DirectionId = Shader.PropertyToID("_CloudShadowDirection");
    private static readonly int HeightId = Shader.PropertyToID("_CloudShadowHeight");
    private static readonly int SunDirectionId = Shader.PropertyToID("_CloudShadowSunDirection");
    private static readonly int TintId = Shader.PropertyToID("_CloudShadowTint");

    private void OnEnable()
    {
        更新全局参数();
    }

    private void Update()
    {
        更新全局参数();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            更新全局参数();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
    }

    private void 更新全局参数()
    {
        Shader.SetGlobalFloat(EnabledId, 启用云影 ? 1f : 0f);
        Shader.SetGlobalFloat(StrengthId, Mathf.Clamp01(云影强度));
        Shader.SetGlobalFloat(ScaleId, Mathf.Max(0.0001f, 云影大小));
        Shader.SetGlobalFloat(CoverageId, Mathf.Clamp01(云层覆盖率));
        Shader.SetGlobalFloat(SoftnessId, Mathf.Max(0.001f, 边缘柔和度));
        Shader.SetGlobalFloat(DetailId, Mathf.Clamp01(细节变化));
        Shader.SetGlobalFloat(SpeedId, Mathf.Max(0f, 流动速度));
        Shader.SetGlobalFloat(HeightId, Mathf.Max(1f, 云层高度));
        Shader.SetGlobalVector(
            DirectionId,
            new Vector4(流动方向.x, 流动方向.y, 0f, 0f)
        );
        Shader.SetGlobalColor(TintId, 阴影色);

        Vector3 sunDirection = 手动太阳方向;
        if (自动跟随场景主光源 && RenderSettings.sun != null)
            sunDirection = -RenderSettings.sun.transform.forward;

        if (sunDirection.sqrMagnitude < 0.0001f)
            sunDirection = Vector3.up;

        sunDirection.Normalize();
        Shader.SetGlobalVector(
            SunDirectionId,
            new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f)
        );
    }
}

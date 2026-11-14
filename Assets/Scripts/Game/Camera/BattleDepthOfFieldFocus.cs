using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 攻击特写期间把景深的对焦点拉到参战单位身上。
/// 只改 DepthOfField.focusDistance，光圈、焦距、模式等保持场景原有配置；
/// 特写结束（EndFocus）时恢复原值。
///
/// 由于特写镜头有持续推进和震屏，对焦点每帧按"相机到目标的视深"重算，
/// 而不是在开始时算一次。
/// </summary>
public class BattleDepthOfFieldFocus : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("对焦距离的跟随速度。0 表示每帧直接贴合，数值越大跟进越快。")]
    private float followSpeed = 12f;

    [SerializeField, Min(0.1f), Tooltip("对焦距离下限，避免镜头贴脸时对焦距离趋近 0。")]
    private float minimumDistance = 1f;

    private DepthOfField _depthOfField;
    private Vector3 _focusPoint;
    private float _currentDistance;
    private float _defaultFocusDistance;
    private bool _defaultFocusOverride;
    private bool _active;

    /// <summary>开始对准某个世界坐标点（通常是参战单位的包围盒中心）。</summary>
    public void BeginFocus(Vector3 worldPoint)
    {
        if (!TryResolveDepthOfField(out DepthOfField depthOfField))
            return;

        _depthOfField = depthOfField;
        _defaultFocusDistance = depthOfField.focusDistance.value;
        _defaultFocusOverride = depthOfField.focusDistance.overrideState;

        _focusPoint = worldPoint;
        _currentDistance = ComputeDistance(worldPoint);
        _active = true;
        Apply(_currentDistance);
    }

    /// <summary>结束对焦，恢复场景原本的景深设置。</summary>
    public void EndFocus()
    {
        if (!_active)
            return;

        _active = false;

        if (_depthOfField != null)
        {
            _depthOfField.focusDistance.value = _defaultFocusDistance;
            _depthOfField.focusDistance.overrideState = _defaultFocusOverride;
        }

        _depthOfField = null;
    }

    private void LateUpdate()
    {
        if (!_active || _depthOfField == null)
            return;

        float target = ComputeDistance(_focusPoint);

        if (followSpeed <= 0f)
        {
            _currentDistance = target;
        }
        else
        {
            // 指数平滑：与帧率无关，镜头推进时焦点跟得住但不会抖。
            float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            _currentDistance = Mathf.Lerp(_currentDistance, target, t);
        }

        Apply(_currentDistance);
    }

    private void Apply(float distance)
    {
        _depthOfField.focusDistance.overrideState = true;
        _depthOfField.focusDistance.value = Mathf.Max(minimumDistance, distance);
    }

    /// <summary>相机到目标点的视深（沿相机前向的投影），与 URP 景深的 focusDistance 定义一致。</summary>
    private static float ComputeDistance(Vector3 worldPoint)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return 0f;

        Transform cameraTransform = camera.transform;
        Vector3 delta = worldPoint - cameraTransform.position;
        return Vector3.Dot(delta, cameraTransform.forward);
    }

    /// <summary>
    /// 找到场景里带 DepthOfField 的 Volume。
    /// 使用 volume.profile（运行时会生成实例副本），不会改动磁盘上的 Volume Profile 资产。
    /// </summary>
    private static bool TryResolveDepthOfField(out DepthOfField depthOfField)
    {
        depthOfField = null;

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || volume.profile == null)
                continue;

            if (volume.profile.TryGet(out DepthOfField candidate) && candidate.active)
            {
                depthOfField = candidate;
                return true;
            }
        }

        return false;
    }
}

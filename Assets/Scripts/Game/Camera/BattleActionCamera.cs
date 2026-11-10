using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 战斗行动特写镜头。
/// 直接控制现有 Battle Camera 虚拟相机，不创建第二台输出相机。
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleActionCamera : MonoBehaviour
{
    [Header("战斗特写镜头")]
    [SerializeField] private CinemachineCamera battleVirtualCamera;
    [SerializeField, Min(0.01f)] private float focusInDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float focusOutDuration = 0.18f;
    [SerializeField, Min(0f)] private float focusHoldDuration = 0.3f;
    [SerializeField, Min(0.1f)] private float minimumFocusDistance = 4.5f;
    [SerializeField, Range(0.5f, 0.95f), Tooltip("参与单位在画面半宽/半高中的最大占比，数值越小留白越多。")]
    private float framePadding = 0.82f;

    [Header("攻击演出")]
    [SerializeField, Min(0f), Tooltip("攻击双方沿地面朝镜头方向额外前移的距离。")]
    private float presentationCameraFrontOffset = 1.25f;

    [Header("命中震屏")]
    [SerializeField, Min(0f)] private float hitShakeStrength = 0.055f;
    [SerializeField, Min(0f)] private float hitShakeDuration = 0.16f;
    [SerializeField, Min(0f)] private float criticalShakeStrength = 0.14f;
    [SerializeField, Min(0f)] private float criticalShakeDuration = 0.24f;

    private Vector3 _battlePosition;
    private Quaternion _battleRotation;
    private bool _initialized;

    public float FocusHoldDuration => focusHoldDuration;

    /// <summary>取得沿地面朝向镜头的偏移，避免角色因相同 Z 坐标而互相遮挡。</summary>
    public Vector3 GetCameraFrontGroundOffset()
    {
        Vector3 towardCamera = Vector3.ProjectOnPlane(
            -(_battleRotation * Vector3.forward), Vector3.up);
        if (towardCamera.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return towardCamera.normalized * presentationCameraFrontOffset;
    }

    /// <summary>在命中结算时震动特写镜头；暴击使用更大的幅度和更长的持续时间。</summary>
    public IEnumerator ShakeImpact(bool isCritical)
    {
        if (!_initialized || !isActiveAndEnabled)
            yield break;

        float strength = isCritical ? criticalShakeStrength : hitShakeStrength;
        float duration = isCritical ? criticalShakeDuration : hitShakeDuration;
        if (strength <= 0f || duration <= 0f)
            yield break;

        Vector3 basePosition = transform.position;
        Vector3 right = _battleRotation * Vector3.right;
        Vector3 up = _battleRotation * Vector3.up;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float damping = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 randomOffset = Random.insideUnitCircle * (strength * damping);
            transform.position = basePosition + right * randomOffset.x + up * randomOffset.y;
            yield return null;
        }

        transform.position = basePosition;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    private void Initialize()
    {
        if (battleVirtualCamera == null)
            battleVirtualCamera = GetComponent<CinemachineCamera>();

        if (battleVirtualCamera == null)
        {
            Debug.LogError("BattleActionCamera：当前对象没有绑定 CinemachineCamera。", this);
            return;
        }

        _battlePosition = transform.position;
        _battleRotation = transform.rotation;
        _initialized = true;
    }

    /// <summary>根据行动者和本次行动的全部实际目标计算构图，并平滑推进镜头。</summary>
    public IEnumerator FocusOnAction(BattleEntity actor, IReadOnlyList<BattleEntity> targets)
    {
        if (!_initialized)
            Initialize();

        if (!_initialized || !TryBuildVisualBounds(actor, targets, out Bounds bounds))
            yield break;

        Vector3 forward = _battleRotation * Vector3.forward;
        float distance = CalculateFocusDistance(bounds);
        Vector3 focusPosition = bounds.center - forward * distance;

        yield return AnimatePosition(focusPosition, focusInDuration);
    }

    /// <summary>将镜头平滑恢复到战斗全景的原始位置。</summary>
    public IEnumerator RestoreBattleView()
    {
        if (!_initialized)
            yield break;

        yield return AnimatePosition(_battlePosition, focusOutDuration);
    }

    private bool TryBuildVisualBounds(
        BattleEntity actor,
        IReadOnlyList<BattleEntity> targets,
        out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        HashSet<BattleEntity> included = new HashSet<BattleEntity>();

        AddEntityBounds(actor, included, ref combinedBounds, ref hasBounds);

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
                AddEntityBounds(targets[i], included, ref combinedBounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void AddEntityBounds(
        BattleEntity entity,
        HashSet<BattleEntity> included,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        if (entity == null || entity.Unit == null || !included.Add(entity))
            return;

        Bounds unitBounds;
        if (!entity.Unit.TryGetVisualBounds(out unitBounds))
            unitBounds = new Bounds(entity.Unit.transform.position, new Vector3(1f, 1.5f, 0.5f));

        if (!hasBounds)
        {
            combinedBounds = unitBounds;
            hasBounds = true;
        }
        else
        {
            combinedBounds.Encapsulate(unitBounds);
        }
    }

    private float CalculateFocusDistance(Bounds bounds)
    {
        float fieldOfView = battleVirtualCamera.Lens.FieldOfView;
        if (fieldOfView <= 1f || fieldOfView >= 179f)
            fieldOfView = 35f;

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float tangentVertical = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tangentHorizontal = tangentVertical * Mathf.Max(0.1f, aspect);
        float padding = Mathf.Clamp(framePadding, 0.5f, 0.95f);

        Vector3 right = _battleRotation * Vector3.right;
        Vector3 up = _battleRotation * Vector3.up;
        Vector3 forward = _battleRotation * Vector3.forward;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float requiredDistance = minimumFocusDistance;

        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 corner = new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z);

            Vector3 offset = corner - bounds.center;
            float horizontalOffset = Mathf.Abs(Vector3.Dot(offset, right));
            float verticalOffset = Mathf.Abs(Vector3.Dot(offset, up));
            float depthOffset = Vector3.Dot(offset, forward);

            float horizontalDistance =
                horizontalOffset / Mathf.Max(0.001f, tangentHorizontal * padding) - depthOffset;
            float verticalDistance =
                verticalOffset / Mathf.Max(0.001f, tangentVertical * padding) - depthOffset;

            requiredDistance = Mathf.Max(requiredDistance, horizontalDistance, verticalDistance);
        }

        return Mathf.Max(minimumFocusDistance, requiredDistance + 0.5f);
    }

    private IEnumerator AnimatePosition(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, easedT);
            yield return null;
        }

        transform.position = targetPosition;
    }

    private void RestoreImmediately()
    {
        if (!_initialized)
            return;

        transform.SetPositionAndRotation(_battlePosition, _battleRotation);
    }
}

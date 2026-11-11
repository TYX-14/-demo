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
    [SerializeField, InspectorName("战斗虚拟相机"), Tooltip("引用 Persistent 场景中 Battle Camera 对象上的 CinemachineCamera。通常保持现有引用即可。")] private CinemachineCamera battleVirtualCamera;
    [SerializeField, InspectorName("镜头推进时间"), Min(0.01f), Tooltip("从战场全景推进到攻击特写所需的时间，单位为秒。数值越小，推进越快。")] private float focusInDuration = 0.22f;
    [SerializeField, InspectorName("镜头复位时间"), Min(0.01f), Tooltip("攻击演出结束后，镜头恢复到战场全景所需的时间，单位为秒。")] private float focusOutDuration = 0.18f;
    [SerializeField, InspectorName("特写额外停留时间"), Min(0f), Tooltip("攻击演出流程结束前，额外保持特写的时间，单位为秒。设为 0 可关闭额外等待。")] private float focusHoldDuration = 0.3f;
    [SerializeField, InspectorName("特写持续推进速度"), Min(0f), Tooltip("特写构图完成后，镜头在特写期间持续向画面深处缓慢推进的速度，单位为世界单位/秒。设为 0 可关闭。")]
    private float focusPushSpeed = 0.18f;
    [SerializeField, InspectorName("特写最小镜头距离"), Min(0.1f), Tooltip("自动构图时允许的最小镜头距离。数值越大，镜头越不容易贴近角色。")] private float minimumFocusDistance = 4.5f;
    [SerializeField, InspectorName("画面留白比例"), Range(0.5f, 0.95f), Tooltip("参与单位在画面半宽和半高中的最大占比。数值越小，角色周围留白越多；数值越大，镜头构图越紧。")]
    private float framePadding = 0.82f;

    [Header("攻击演出")]
    [SerializeField, InspectorName("角色朝镜头前移距离"), Min(0f), Tooltip("攻击者与目标的交战位置沿地面朝镜头方向整体前移的距离。数值越大，角色越靠近镜头。")]
    private float presentationCameraFrontOffset = 1.25f;

    [Header("命中震屏")]
    [SerializeField, InspectorName("普通命中震屏幅度"), Min(0f), Tooltip("普通命中时镜头随机震动的幅度。设为 0 可关闭普通命中震屏。")] private float hitShakeStrength = 0.055f;
    [SerializeField, InspectorName("普通命中震屏时长"), Min(0f), Tooltip("普通命中震屏持续的时间，单位为秒。")] private float hitShakeDuration = 0.16f;
    [SerializeField, InspectorName("暴击震屏幅度"), Min(0f), Tooltip("暴击命中时镜头震动的幅度。需要后续接入暴击判定后才会生效。")] private float criticalShakeStrength = 0.14f;
    [SerializeField, InspectorName("暴击震屏时长"), Min(0f), Tooltip("暴击命中震屏持续的时间，单位为秒。需要后续接入暴击判定后才会生效。")] private float criticalShakeDuration = 0.24f;
    [SerializeField, InspectorName("普通命中旋转角度"), Range(0f, 15f), Tooltip("普通命中后镜头倾斜的角度。建议从 4 度左右开始调整。")]
    private float hitFollowThroughRotation = 4f;
    [SerializeField, InspectorName("暴击命中旋转角度"), Range(0f, 20f), Tooltip("暴击命中后镜头倾斜的角度。需要接入暴击判定后才会生效。")]
    private float criticalFollowThroughRotation = 6f;
    [SerializeField, InspectorName("普通命中水平偏移"), Min(0f), Tooltip("普通命中后镜头沿攻击方向产生的水平偏移量，单位为世界单位。")]
    private float hitFollowThroughHorizontal = 0.16f;
    [SerializeField, InspectorName("普通命中垂直偏移"), Min(0f), Tooltip("普通命中后镜头沿攻击方向产生的垂直偏移量，单位为世界单位。")]
    private float hitFollowThroughVertical = 0.08f;
    [SerializeField, InspectorName("普通命中深度偏移"), Min(0f), Tooltip("普通命中后镜头沿自身朝向产生的前后偏移量，单位为世界单位。")]
    private float hitFollowThroughDepth = 0.10f;
    [SerializeField, InspectorName("暴击偏移倍率"), Min(1f), Tooltip("暴击时水平、垂直和深度偏移相对于普通命中的倍率。")]
    private float criticalFollowThroughMultiplier = 1.35f;
    [SerializeField, InspectorName("冲击姿态过渡时间"), Min(0f), Tooltip("镜头从震屏状态过渡到倾斜及偏移姿态所需的时间，单位为秒。")]
    private float hitFollowThroughDuration = 0.16f;

    private Vector3 _battlePosition;
    private Quaternion _battleRotation;
    private bool _initialized;
    private bool _focusPushActive;

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

    /// <summary>
    /// 命中瞬间同时启动冲击位移、旋转与短促震动。
    /// 根据攻击者和目标的 X 坐标判断左右方向，整个过程连续过渡到最终冲击姿态。
    /// </summary>
    public IEnumerator ShakeImpact(bool isCritical, float actorX, float targetX)
    {
        if (!_initialized || !isActiveAndEnabled)
            yield break;

        float strength = isCritical ? criticalShakeStrength : hitShakeStrength;
        float duration = isCritical ? criticalShakeDuration : hitShakeDuration;
        if (duration <= 0f)
            yield break;

        Vector3 basePosition = transform.position;
        Quaternion baseRotation = transform.rotation;
        Vector3 right = baseRotation * Vector3.right;
        Vector3 up = baseRotation * Vector3.up;
        Vector3 forward = baseRotation * Vector3.forward;

        // 直接比较世界 X 坐标，明确判断目标位于攻击者左侧还是右侧。
        float horizontalSign = Mathf.Sign(targetX - actorX);
        if (Mathf.Approximately(horizontalSign, 0f))
            horizontalSign = 1f;

        float multiplier = isCritical ? criticalFollowThroughMultiplier : 1f;
        float rotationAngle = isCritical
            ? criticalFollowThroughRotation
            : hitFollowThroughRotation;

        // 最终保留的冲击姿态。位移方向跟随目标所在的左右方向。
        Vector3 finalOffset =
            right * (hitFollowThroughHorizontal * multiplier * horizontalSign) +
            up * (hitFollowThroughVertical * multiplier) +
            forward * (hitFollowThroughDepth * multiplier);
        float rotationSign = -horizontalSign;
        Quaternion finalRotation = Quaternion.AngleAxis(
            rotationAngle * rotationSign, forward) * baseRotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 从命中第一刻就启动主冲击：先快速受力到峰值，再自然衰减到最终姿态。
            float settle = t * t * (3f - 2f * t);
            float impulse = 4f * t * (1f - t);
            Vector3 mainOffset = finalOffset * (settle + impulse * 0.65f);
            float rotationProgress = settle + impulse * 0.30f;
            Quaternion mainRotation = Quaternion.Slerp(
                baseRotation, finalRotation, Mathf.Clamp01(rotationProgress));

            // 叠加幅度较小、快速衰减的连续振动，不再逐帧随机跳方向。
            float damping = (1f - t) * (1f - t);
            float vibrationX = Mathf.Sin(elapsed * 92f) * strength * damping;
            float vibrationY = Mathf.Sin(elapsed * 117f + 0.8f) * strength * damping * 0.65f;

            transform.position = basePosition + mainOffset +
                right * vibrationX + up * vibrationY;
            transform.rotation = mainRotation;
            yield return null;
        }

        // 精确停在最终姿态，避免循环最后一帧留下细微误差。
        transform.SetPositionAndRotation(basePosition + finalOffset, finalRotation);
    }

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (!_focusPushActive || !_initialized || focusPushSpeed <= 0f)
            return;

        // 在其他镜头协程完成本帧位置更新后，再沿镜头朝向叠加微小推进。
        transform.position += transform.forward * (focusPushSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>特写构图完成后，开始持续向前缓慢推进。</summary>
    public void BeginFocusPush()
    {
        if (_initialized && isActiveAndEnabled)
            _focusPushActive = true;
    }

    /// <summary>停止持续推进，随后即可安全执行镜头复位。</summary>
    public void EndFocusPush()
    {
        _focusPushActive = false;
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

        EndFocusPush();
        yield return AnimatePose(_battlePosition, _battleRotation, focusOutDuration);
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
        yield return AnimatePose(targetPosition, transform.rotation, duration);
    }

    private IEnumerator AnimatePose(
        Vector3 targetPosition,
        Quaternion targetRotation,
        float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, easedT);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
            yield return null;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void RestoreImmediately()
    {
        EndFocusPush();
        if (!_initialized)
            return;

        transform.SetPositionAndRotation(_battlePosition, _battleRotation);
    }
}

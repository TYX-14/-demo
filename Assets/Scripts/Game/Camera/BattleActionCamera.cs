using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 战斗行动特写镜头。直接控制 Persistent 场景中的 Battle Camera 虚拟相机，不创建第二台输出相机。
///
/// 相机姿态只有两个来源，互斥：
/// 1. 协程动画（聚焦 / 复位）—— 每帧直接写 transform；
/// 2. 命中姿态保持（ImpactHeld）—— 由 LateUpdate 每帧重写，直到 EndFocusPush / RestoreBattleView。
/// 这样命中姿态不会因为其他系统在同一帧写 transform 而被抹掉。
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
    [SerializeField, InspectorName("冲击姿态过渡时间"), Min(0f), Tooltip("镜头从震屏状态过渡到倾斜及偏移姿态所需的时间，单位为秒。目前由震屏时长驱动，保留字段用于兼容。")]
    private float hitFollowThroughDuration = 0.16f;

    private Vector3 _battlePosition;
    private Quaternion _battleRotation;
    private bool _initialized;

    // 命中姿态保持：由 LateUpdate 每帧重写，直到被 EndFocusPush / RestoreBattleView 释放。
    private bool _impactHeld;
    private Vector3 _impactPosition;
    private Quaternion _impactRotation;

    // 特写期间的持续推进。
    private bool _pushActive;
    private float _pushDistance;

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

    private void Awake()
    {
        Initialize();
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    /// <summary>
    /// 命中姿态优先：只要处于保持状态，每帧都重写 transform，
    /// 因此相机不会被其他系统在同一帧改回去。
    /// </summary>
    private void LateUpdate()
    {
        if (!_initialized)
            return;

        if (_pushActive && focusPushSpeed > 0f)
            _pushDistance += focusPushSpeed * Time.unscaledDeltaTime;

        if (_impactHeld)
        {
            ApplyImpactPose();
            return;
        }

        if (_pushActive && focusPushSpeed > 0f)
            transform.position += transform.forward * (focusPushSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 命中冲击：镜头沿攻击方向轻微平移，并按 tiltDirection 倾斜，最后定格在冲击姿态。
    /// actorScreenX / targetScreenX 是屏幕坐标，只用于决定位移与振动的方向；
    /// tiltDirection 由调用方按阵营给出（当前战场固定为「敌方在左、我方在右」）。
    /// </summary>
    public IEnumerator ShakeImpact(bool isCritical, float actorScreenX, float targetScreenX, float tiltDirection)
    {
        if (!_initialized || !isActiveAndEnabled)
            yield break;

        float duration = isCritical ? criticalShakeDuration : hitShakeDuration;
        if (duration <= 0f)
            yield break;

        float strength = isCritical ? criticalShakeStrength : hitShakeStrength;
        float multiplier = isCritical ? criticalFollowThroughMultiplier : 1f;
        float tiltAngle = (isCritical ? criticalFollowThroughRotation : hitFollowThroughRotation) * multiplier;

        // 攻击方向：+1 表示目标在画面右侧，-1 表示在左侧；两者重合时按向右处理。
        float attackDirection = Mathf.Sign(targetScreenX - actorScreenX);
        if (Mathf.Approximately(attackDirection, 0f))
            attackDirection = 1f;

        // 倾斜方向由战场阵营规则明确传入，与屏幕坐标无关。
        // 水平偏移和震动仍使用 attackDirection，保持原有攻击方向反馈。

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 当前阵型固定为「敌方在左、我方在右」，此时阵营方向应恒等于「−攻击方向」。
        // 一旦两者不一致，说明阵型、站位或镜头朝向已经偏离假设，调用方需要同步修改。
        if (Mathf.Abs(targetScreenX - actorScreenX) > 1f &&
            !Mathf.Approximately(tiltDirection, -attackDirection))
        {
            Debug.LogWarning(
                $"[镜头倾斜] 阵营方向({tiltDirection})与画面攻击方向({attackDirection})不一致，" +
                "请检查战斗阵型是否仍满足「敌方在左、我方在右」。", this);
        }
#endif

        Vector3 restPosition = transform.position;
        Quaternion restRotation = transform.rotation;
        Vector3 right = restRotation * Vector3.right;
        Vector3 up = restRotation * Vector3.up;
        Vector3 forward = restRotation * Vector3.forward;

        // 最终冲击姿态：位置顺攻击方向平移，朝向绕镜头前向轴旋转。
        Vector3 positionOffset =
            right * (hitFollowThroughHorizontal * multiplier * attackDirection) +
            up * (hitFollowThroughVertical * multiplier) +
            forward * (hitFollowThroughDepth * multiplier);

        Vector3 impactPosition = restPosition + positionOffset;
        Quaternion impactRotation = Quaternion.AngleAxis(tiltAngle * tiltDirection, forward) * restRotation;

        // 从静止快速受力到峰值，再衰减到最终姿态。
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float settle = t * t * (3f - 2f * t);
            float impulse = 4f * t * (1f - t);
            float positionProgress = settle + impulse * 0.65f;
            float rotationProgress = Mathf.Clamp01(settle + impulse * 0.30f);

            // 振动同样跟随攻击方向，避免出现方向固定的偏置。
            float damping = (1f - t) * (1f - t);
            Vector3 vibration =
                right * (Mathf.Sin(elapsed * 92f) * strength * damping * attackDirection) +
                up * (Mathf.Sin(elapsed * 117f + 0.8f) * strength * damping * 0.65f);

            transform.position = restPosition + positionOffset * positionProgress + vibration;
            transform.rotation = Quaternion.Slerp(restRotation, impactRotation, rotationProgress);
            yield return null;
        }

        HoldImpactPose(impactPosition, impactRotation);
    }

    /// <summary>锁定命中姿态，交给 LateUpdate 每帧维持。</summary>
    private void HoldImpactPose(Vector3 position, Quaternion rotation)
    {
        _impactPosition = position;
        _impactRotation = rotation;
        _impactHeld = true;
        ApplyImpactPose();
    }

    private void ApplyImpactPose()
    {
        Vector3 pushDirection = _impactRotation * Vector3.forward;
        transform.SetPositionAndRotation(_impactPosition + pushDirection * _pushDistance, _impactRotation);
    }

    private void ReleaseImpactPose()
    {
        _impactHeld = false;
    }

    /// <summary>特写构图完成后，开始持续向前缓慢推进。</summary>
    public void BeginFocusPush()
    {
        if (!_initialized || !isActiveAndEnabled)
            return;

        _pushActive = true;
        _pushDistance = 0f;
    }

    /// <summary>停止持续推进并释放命中姿态，随后即可安全执行镜头复位。</summary>
    public void EndFocusPush()
    {
        _pushActive = false;
        ReleaseImpactPose();
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

        // 新一次特写开始前，先释放上一次可能残留的命中姿态。
        ReleaseImpactPose();
        _pushDistance = 0f;

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
        _pushActive = false;
        _pushDistance = 0f;
        ReleaseImpactPose();

        if (!_initialized)
            return;

        transform.SetPositionAndRotation(_battlePosition, _battleRotation);
    }
}

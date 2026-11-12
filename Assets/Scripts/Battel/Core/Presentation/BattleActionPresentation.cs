using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 一次行动的演出编排：前摇、目标提示、镜头推进与复位、交战摆位、震屏、命中间隔、收尾还原。
/// 只负责“按什么顺序演”，不判断规则、不修改战斗数据、不切换状态。
///
/// 调用约定：
/// 1. 每个行动新建一个实例，行动结束即丢弃，不跨行动缓存任何东西；
/// 2. 结算由调用方在 PlayApproach 与 PlayImpactAndOutro 之间执行；
/// 3. 内部的 IEnumerator 由调用方（状态机）驱动，本类不持有协程。
/// </summary>
public sealed class BattleActionPresentation
{
    private readonly BattleController _controller;
    private readonly BattleActionContext _actionContext;

    private readonly bool _isEnemyAction;
    private readonly List<BattleUnit> _presentedTargets = new();

    private BattleActionCamera _actionCamera;
    private bool _cameraFocusStarted;
    private BattleUnit _presentationActor;
    private bool _actionPresentationStarted;
    private Vector3 _cameraImpactDirection;
    private float _impactActorScreenX;
    private float _impactTargetScreenX;

    public BattleActionPresentation(BattleController controller, BattleActionContext actionContext)
    {
        _controller = controller;
        _actionContext = actionContext;
        _isEnemyAction = actionContext?.Actor != null && !actionContext.Actor.IsPlayer;
    }

    /// <summary>
    /// 前摇 → 目标提示 → 镜头推进 → 交战摆位。
    /// 返回后调用方应立即执行技能结算，再调用 PlayImpactAndOutro。
    /// </summary>
    public IEnumerator PlayApproach()
    {
        _presentedTargets.Clear();

        // 敌方行动在本次实际命中的目标头上开启浮动小手，作为攻击预警。
        if (_isEnemyAction)
            SetEnemyTargetCursors(true);

        // 敌方先完成攻击预警与前摇，再进入特写；避免镜头在攻击尚未开始时提前推进。
        if (_isEnemyAction && _controller.EnemyActionWindupDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionWindupDelay);

        // 战斗场景可能被卸载重载，因此每次行动重新查找，不缓存到字段。
        _actionCamera = Object.FindAnyObjectByType<BattleActionCamera>();
        _cameraFocusStarted = _actionCamera != null && _actionContext.ResolvedTargets.Count > 0;

        if (_cameraFocusStarted)
        {
            // 角色正式进入攻击特写，行动已经开始；此时不再显示行动者光圈或目标小手。
            if (BattleVFXManager.Instance != null)
                BattleVFXManager.Instance.HideActionHighlights();

            if (_isEnemyAction)
                SetEnemyTargetCursors(false);

            yield return _actionCamera.FocusOnAction(_actionContext.Actor, _actionContext.ResolvedTargets);
        }

        if (_cameraFocusStarted)
            _actionCamera.BeginFocusPush();

        StageUnits();
    }

    /// <summary>震屏 → 命中停顿 → 特写停留 → 清飘字 → 还原单位与镜头。</summary>
    public IEnumerator PlayImpactAndOutro(bool criticalHit, bool damageEventTriggered)
    {
        // 振屏依据是否发生有效伤害命中，而不是伤害数值是否大于 0。
        // 0 点伤害仍然是命中；只有闪避等未命中情况才不应触发伤害事件与振屏。
        IEnumerator shake = null;
        if (damageEventTriggered && _cameraFocusStarted && _actionCamera != null)
            shake = _actionCamera.ShakeImpact(criticalHit, _impactActorScreenX, _impactTargetScreenX, _isEnemyAction ? -1f : 1f);

        // 滑动是命中之后的惯性：与震屏同帧开始，不推迟命中帧。
        yield return RunTogether(shake, PlaySlide());

        // 命中后保留短暂停顿，但不提前恢复攻击者与目标的视觉状态。
        // 攻击图片和目标冻结姿势会一直维持到特写正式结束。
        if (_actionPresentationStarted || _presentedTargets.Count > 0)
            yield return new WaitForSecondsRealtime(0.12f);

        // 额外特写时间也属于攻击图片展示时间，结束特写前才恢复角色 Animator。
        if (_cameraFocusStarted && _actionCamera != null && _actionCamera.FocusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(_actionCamera.FocusHoldDuration);

        // 伤害飘字的生命周期不能越过攻击特写；镜头复位前统一清理。
        _controller.ClearDamagePopups();

        if (_actionPresentationStarted && _presentationActor != null)
            _presentationActor.EndActionPresentation();

        foreach (BattleUnit presentedTarget in _presentedTargets)
            presentedTarget.EndActionPresentation();

        // 先停止持续推进，再恢复战场全景，最后执行前进/后退等后置位移。
        if (_cameraFocusStarted && _actionCamera != null)
            _actionCamera.EndFocusPush();

        if (_cameraFocusStarted && _actionCamera != null && _actionCamera.isActiveAndEnabled)
            yield return _actionCamera.RestoreBattleView();
    }

    /// <summary>敌方行动的恢复延迟与目标提示收尾。</summary>
    public IEnumerator PlayRecovery()
    {
        if (_isEnemyAction && _controller.EnemyActionRecoveryDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionRecoveryDelay);

        if (_isEnemyAction)
            SetEnemyTargetCursors(false);
    }

    /// <summary>
    /// 仅调整视觉 Transform：把攻击者与本次全部目标临时移到镜头前，
    /// BattleEntity 与阵型数据一律不动，由 EndActionPresentation 还原。
    /// </summary>
    private void StageUnits()
    {
        _presentationActor = _actionContext.Actor.Unit;

        // 以第一个有效且非自身的目标确定交战方向；其余 AoE 目标沿用同一位移量，
        // 这样既能一起前移，也能保留原本的相对阵型，不会全部挤到同一个点。
        BattleUnit presentationTarget = _actionContext.ResolvedTargets
            .Select(target => target != null ? target.Unit : null)
            .FirstOrDefault(unit => unit != null && unit != _presentationActor);

        // 命中方向用“屏幕左右”而不是世界 X：镜头如果带偏航，世界 X 和画面左右会不一致。
        // ShakeImpact 只用这两个值判断符号，因此传屏幕 X 是安全的。
        Camera renderCamera = Camera.main;
        if (renderCamera != null && _presentationActor != null && presentationTarget != null)
        {
            _impactActorScreenX = renderCamera.WorldToScreenPoint(_presentationActor.transform.position).x;
            _impactTargetScreenX = renderCamera.WorldToScreenPoint(presentationTarget.transform.position).x;
        }
        else
        {
            _impactActorScreenX = _presentationActor != null ? _presentationActor.transform.position.x : 0f;
            _impactTargetScreenX = presentationTarget != null
                ? presentationTarget.transform.position.x
                : _impactActorScreenX + 1f;
        }

        _cameraImpactDirection = Vector3.zero;
        if (_presentationActor != null && presentationTarget != null)
        {
            _cameraImpactDirection = Vector3.ProjectOnPlane(
                presentationTarget.transform.position - _presentationActor.transform.position,
                Vector3.up);
            if (_cameraImpactDirection.sqrMagnitude < 0.0001f)
                _cameraImpactDirection = _presentationActor.transform.forward;
            _cameraImpactDirection.Normalize();
        }

        // 没有攻击图片时不进入交战摆位，单位留在原站位。
        if (_actionContext.Skill.AttackSprite == null ||
            _presentationActor == null || presentationTarget == null)
            return;

        Vector3 actorPosition = _presentationActor.transform.position;
        Vector3 targetPosition = presentationTarget.transform.position;
        Vector3 direction = _cameraImpactDirection.sqrMagnitude > 0.0001f
            ? _cameraImpactDirection
            : Vector3.ProjectOnPlane(targetPosition - actorPosition, Vector3.up).normalized;

        float actorRadius = 0.25f;
        float targetRadius = 0.25f;
        if (_presentationActor.TryGetVisualBounds(out Bounds actorBounds))
            actorRadius = Mathf.Abs(direction.x) * actorBounds.extents.x +
                          Mathf.Abs(direction.z) * actorBounds.extents.z;
        if (presentationTarget.TryGetVisualBounds(out Bounds targetBounds))
            targetRadius = Mathf.Abs(direction.x) * targetBounds.extents.x +
                           Mathf.Abs(direction.z) * targetBounds.extents.z;

        bool isFriendlyAoe =
            _actionContext.Skill.TargetRelation == SkillTargetRelation.Ally &&
            _actionContext.Skill.TargetType == SkillTargetType.AoE;

        Vector3 engagementCenter = (actorPosition + targetPosition) * 0.5f;
        float gap = 0.05f;
        Vector3 cameraFrontOffset = _actionCamera != null
            ? _actionCamera.GetCameraFrontGroundOffset()
            : Vector3.zero;

        // 友方 AoE 不使用近战交战中点，只沿世界 Z 轴整体前移。
        // 保留镜头朝向所决定的前后符号，但不改变任何单位的 X/Y 坐标。
        Vector3 friendlyAoeOffset = new(0f, 0f, cameraFrontOffset.z);
        Vector3 actorEngagementPosition = isFriendlyAoe
            ? actorPosition + friendlyAoeOffset
            : engagementCenter - direction * (actorRadius + gap * 0.5f) + cameraFrontOffset;
        Vector3 targetEngagementPosition = isFriendlyAoe
            ? targetPosition + friendlyAoeOffset
            : engagementCenter + direction * (targetRadius + gap * 0.5f) + cameraFrontOffset;

        _actionPresentationStarted = _presentationActor.BeginActionPresentation(
            _actionContext.Skill.AttackSprite, actorEngagementPosition);

        if (!_actionPresentationStarted)
            return;

        // 普通攻击将目标组拉到交战位置；友方 AoE 则让施法者与所有受影响队友
        // 沿同一个世界 Z 轴偏移量整体前移，保持四人的相对站位不变。
        Vector3 targetGroupOffset = isFriendlyAoe
            ? friendlyAoeOffset
            : targetEngagementPosition - targetPosition;

        foreach (BattleEntity resolvedTarget in _actionContext.ResolvedTargets)
        {
            BattleUnit targetUnit = resolvedTarget != null ? resolvedTarget.Unit : null;
            if (targetUnit == null || targetUnit == _presentationActor ||
                _presentedTargets.Contains(targetUnit))
                continue;

            Vector3 targetPresentationPosition = targetUnit.transform.position + targetGroupOffset;
            if (targetUnit.BeginActionPresentation(null, targetPresentationPosition))
                _presentedTargets.Add(targetUnit);
        }

        // 近战攻击图片就是命中帧，因此不在这里等待，下一步立即结算伤害并触发震屏。
    }

    /// <summary>
    /// 同时推进两个 IEnumerator。两个子协程都只允许 yield return null，
    /// 不接受嵌套的等待对象（本方法不会转发 Current）。
    /// </summary>
    private static IEnumerator RunTogether(IEnumerator first, IEnumerator second)
    {
        bool firstRunning = first != null;
        bool secondRunning = second != null;

        while (firstRunning || secondRunning)
        {
            if (firstRunning)
                firstRunning = first.MoveNext();
            if (secondRunning)
                secondRunning = second.MoveNext();

            if (firstRunning || secondRunning)
                yield return null;
        }
    }

    /// <summary>
    /// 命中后的惯性滑动：让攻击者与被攻击者沿 ±攻击方向轻微移动，表现攻击产生的后坐与推挤。
    /// 只改演出位置，特写结束由 EndActionPresentation 统一还原；
    /// 没有进入交战摆位的技能（没有攻击图片）不会滑动。
    /// </summary>
    private IEnumerator PlaySlide()
    {
        SkillSlideConfig slide = _actionContext.Skill.Slide;
        if (slide == null || !slide.HasAnySlide || !_actionPresentationStarted)
            yield break;

        if (_presentationActor == null || _cameraImpactDirection.sqrMagnitude < 0.0001f)
            yield break;

        float actorSign = ToSlideSign(slide.attacker);
        float targetSign = ToSlideSign(slide.targets);
        if (actorSign == 0f && targetSign == 0f)
            yield break;

        Vector3 direction = _cameraImpactDirection;
        float distance = Mathf.Max(0f, slide.distance);
        float duration = Mathf.Max(0.01f, slide.outDuration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 先快后慢：起手有冲劲，收尾稳下来。
            float eased = 1f - (1f - t) * (1f - t);
            ApplySlideOffset(direction, distance * eased, actorSign, targetSign);
            yield return null;
        }

        ApplySlideOffset(direction, distance, actorSign, targetSign);
    }

    private void ApplySlideOffset(Vector3 direction, float distance, float actorSign, float targetSign)
    {
        if (actorSign != 0f && _presentationActor != null)
            _presentationActor.SetActionSlideOffset(direction * (distance * actorSign));

        if (targetSign == 0f)
            return;

        for (int i = 0; i < _presentedTargets.Count; i++)
        {
            BattleUnit unit = _presentedTargets[i];
            if (unit != null)
                unit.SetActionSlideOffset(direction * (distance * targetSign));
        }
    }

    private static float ToSlideSign(SkillSlideMode mode)
    {
        return mode switch
        {
            SkillSlideMode.AlongAttack => 1f,
            SkillSlideMode.AgainstAttack => -1f,
            _ => 0f
        };
    }

    /// <summary>敌方行动预告：在本次实际命中的目标头上开启或收起浮动小手。</summary>
    private void SetEnemyTargetCursors(bool visible)
    {
        IReadOnlyList<BattleEntity> targets = _actionContext?.ResolvedTargets;
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            BattleUnit unit = targets[i] != null ? targets[i].Unit : null;
            if (unit == null)
                continue;

            unit.SetTargetCursorMode(visible
                ? BattleUnit.TargetCursorMode.Hovered
                : BattleUnit.TargetCursorMode.Hidden);
        }
    }
}

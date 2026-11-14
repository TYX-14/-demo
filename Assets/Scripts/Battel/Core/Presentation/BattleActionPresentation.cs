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

    // 命中后惯性滑动的运行时状态。滑动自行计时推进，不阻塞特写的时间线。
    private bool _slideActive;
    private float _slideElapsed;
    private float _slideDuration;
    private float _slideDistance;
    private Vector3 _slideDirection;
    private float _slideActorSign;
    private float _slideTargetSign;
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
            if (_isEnemyAction)
                SetEnemyTargetCursors(false);

            // 统一特写入口：收起光圈与常驻 HUD、对准参战者、开始持续推进。
            yield return BattleCloseup.Enter(_actionContext.Actor, _actionContext.ResolvedTargets);
        }

        StageUnits();
    }

    /// <summary>震屏 → 命中停顿 → 特写停留 → 清飘字 → 还原单位与镜头。</summary>
    public IEnumerator PlayImpactAndOutro(bool criticalHit, bool damageEventTriggered)
    {
        // 振屏依据是否发生有效伤害命中，而不是伤害数值是否大于 0。
        // 0 点伤害仍然是命中；只有闪避等未命中情况才不应触发伤害事件与振屏。
        IEnumerator shake = null;
        // 固定阵型：我方在右、敌方在左，因此倾斜方向直接由阵营决定，不依赖屏幕坐标。
        float tiltDirection = _isEnemyAction ? -1f : 1f;
        if (damageEventTriggered && _cameraFocusStarted && _actionCamera != null)
            shake = _actionCamera.ShakeImpact(criticalHit, _impactActorScreenX, _impactTargetScreenX, tiltDirection);

        // 滑动是命中之后的惯性：与震屏同帧开始，但绝不进入等待队列。
        // 它自己按时间推进，震屏结束时可能还没滑完，由后面的停顿继续推进。
        BeginSlide();

        if (shake != null)
        {
            while (shake.MoveNext())
            {
                AdvanceSlide(Time.unscaledDeltaTime);
                yield return null;
            }
        }

        // 命中后保留短暂停顿，但不提前恢复攻击者与目标的视觉状态。
        // 攻击图片和目标冻结姿势会一直维持到特写正式结束。
        if (_actionPresentationStarted || _presentedTargets.Count > 0)
            yield return WaitAdvancingSlide(0.12f);

        // 额外特写时间也属于攻击图片展示时间，结束特写前才恢复角色 Animator。
        if (_cameraFocusStarted && _actionCamera != null && _actionCamera.FocusHoldDuration > 0f)
            yield return WaitAdvancingSlide(_actionCamera.FocusHoldDuration);

        // 特写比滑动短时，直接把滑动补到终点，避免还原位置时出现跳变。
        FinishSlide();

        // 伤害飘字的生命周期不能越过攻击特写；镜头复位前统一清理。
        _controller.ClearDamagePopups();

        if (_actionPresentationStarted && _presentationActor != null)
            _presentationActor.EndActionPresentation();

        foreach (BattleUnit presentedTarget in _presentedTargets)
            presentedTarget.EndActionPresentation();

        // 先停止持续推进，再恢复战场全景与 HUD，最后执行前进/后退等后置位移。
        if (_cameraFocusStarted)
            yield return BattleCloseup.Exit();
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
        // 单体自选技能（例如队伍只有一人时对自己使用协同开火）没有其他目标，
        // 此时退化为以自身为目标，否则整段攻击演出会被下面的守卫直接跳过。
        BattleUnit otherTarget = _actionContext.ResolvedTargets
            .Select(target => target != null ? target.Unit : null)
            .FirstOrDefault(unit => unit != null && unit != _presentationActor);

        bool isSelfPresentation = otherTarget == null;
        BattleUnit presentationTarget = isSelfPresentation ? _presentationActor : otherTarget;

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

        // 攻击者与目标重合时（自选技能）没有水平方向，退回自身朝向，并压平到地面，
        // 避免把角色沿 Y 轴推歪。
        _cameraImpactDirection = Vector3.zero;
        if (_presentationActor != null && presentationTarget != null)
        {
            _cameraImpactDirection = Vector3.ProjectOnPlane(
                presentationTarget.transform.position - _presentationActor.transform.position,
                Vector3.up);
            if (_cameraImpactDirection.sqrMagnitude < 0.0001f)
                _cameraImpactDirection = Vector3.ProjectOnPlane(
                    _presentationActor.transform.forward, Vector3.up);
            if (_cameraImpactDirection.sqrMagnitude < 0.0001f)
                _cameraImpactDirection = Vector3.forward;
            _cameraImpactDirection.Normalize();
        }

        // 没有攻击图片时不进入交战摆位，单位留在原站位。
        if (_actionContext.Skill.AttackSprite == null || _presentationActor == null)
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
    /// 开始命中后的惯性滑动：让攻击者与被攻击者沿 ±攻击方向轻微移动，表现攻击产生的后坐与推挤。
    /// 只记录参数，不产生等待；真正的推进由 AdvanceSlide 每帧完成。
    /// 没有进入交战摆位的技能（没有攻击图片）不会滑动。
    /// </summary>
    private void BeginSlide()
    {
        _slideActive = false;
        _slideElapsed = 0f;

        SkillDefinitionSO skill = _actionContext.Skill;
        SkillSlideConfig slide = skill != null ? skill.Slide : null;
        if (slide == null || !slide.HasAnySlide || !_actionPresentationStarted)
            return;

        if (_presentationActor == null || _cameraImpactDirection.sqrMagnitude < 0.0001f)
            return;

        float actorSign = ToSlideSign(slide.attacker);
        float targetSign = ToSlideSign(slide.targets);
        if (actorSign == 0f && targetSign == 0f)
            return;

        _slideDirection = _cameraImpactDirection;
        _slideDistance = Mathf.Max(0f, slide.distance);
        _slideDuration = Mathf.Max(0.01f, slide.outDuration);
        _slideActorSign = actorSign;
        _slideTargetSign = targetSign;
        _slideActive = true;
    }

    /// <summary>按时间推进滑动。不等待，调用方在自己的循环里喂 deltaTime。</summary>
    private void AdvanceSlide(float deltaTime)
    {
        if (!_slideActive)
            return;

        _slideElapsed += deltaTime;
        float t = Mathf.Clamp01(_slideElapsed / _slideDuration);
        // 先快后慢：起手有冲劲，收尾稳下来。
        float eased = 1f - (1f - t) * (1f - t);
        ApplySlideOffset(_slideDirection, _slideDistance * eased, _slideActorSign, _slideTargetSign);

        if (t >= 1f)
            _slideActive = false;
    }

    /// <summary>特写结束时若滑动仍未走完，直接补到终点，避免还原位置时跳变。</summary>
    private void FinishSlide()
    {
        if (!_slideActive)
            return;

        ApplySlideOffset(_slideDirection, _slideDistance, _slideActorSign, _slideTargetSign);
        _slideActive = false;
    }

    /// <summary>等待指定时长，同时让滑动继续推进，不额外拉长演出。</summary>
    private IEnumerator WaitAdvancingSlide(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            AdvanceSlide(deltaTime);
            yield return null;
        }
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

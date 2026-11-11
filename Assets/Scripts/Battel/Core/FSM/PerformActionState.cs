using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>统一执行已确认的玩家或敌方行动，并在结算后交还给 CTB 调度器。</summary>
public class PerformActionState : BattleState
{
    private readonly BattleActionContext _actionContext;

    public PerformActionState(BattleController controller, BattleActionContext actionContext)
        : base(controller)
    {
        _actionContext = actionContext;
    }

    public override IEnumerator Execute()
    {
        if (_actionContext == null ||
            _actionContext.Actor == null ||
            _actionContext.Actor.RuntimeData == null ||
            _actionContext.Skill == null ||
            _actionContext.ResolvedTargets == null ||
            (_actionContext.ResolvedTargets.Count == 0 &&
             _actionContext.Skill.TargetSelectionMode != SkillTargetSelectionMode.None))
        {
            Debug.LogWarning("PerformActionState：行动数据不完整或没有有效目标。");
            _controller.RejectUnstartedAction(_actionContext);
            yield break;
        }

        if (_actionContext.Actor.RuntimeData.CurrentSP < _actionContext.Skill.SpCost)
        {
            Debug.LogWarning(
                $"SP 不足：{_actionContext.Actor.Definition.Name} 需要 {_actionContext.Skill.SpCost} SP，" +
                $"当前只有 {_actionContext.Actor.RuntimeData.CurrentSP} SP。");
            _controller.RejectUnstartedAction(_actionContext);
            yield break;
        }

        if (!_controller.TryBeginAction(_actionContext))
        {
            Debug.LogWarning("PerformActionState：行动未能启动，可能已被取消、重复提交或行动者已变化。");
            _controller.RejectUnstartedAction(_actionContext);
            yield break;
        }

        if (_actionContext.Skill.SpCost > 0)
            _actionContext.Actor.RuntimeData.ModifyMp(-_actionContext.Skill.SpCost);

        string actorName = _actionContext.Actor.Definition != null
            ? _actionContext.Actor.Definition.Name
            : _actionContext.Actor.ID;
        string targetNames = string.Join(", ", _actionContext.ResolvedTargets.Select(
            target => target != null
                ? (target.Definition != null ? target.Definition.Name : target.ID)
                : "无效目标"));

        Debug.Log($"[行动开始] {actorName} 使用技能「{_actionContext.Skill.SkillName}」；" +
                  $"输入位置：{_actionContext.SelectedInputPosition}；实际目标：{targetNames}。");

        bool isEnemyAction = !_actionContext.Actor.IsPlayer;
        if (isEnemyAction && BattleVFXManager.Instance != null)
            BattleVFXManager.Instance.ShowEnemyActionTargets(_actionContext.ResolvedTargets);

        // 敌方先完成攻击预警与前摇，再进入特写；避免镜头在攻击尚未开始时提前推进。
        // 玩家与敌方从这里开始共用完全相同的镜头特写和命中演出时序。
        if (isEnemyAction && _controller.EnemyActionWindupDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionWindupDelay);

        // 所有行动者与本次行动的全部实际目标同时进入特写；没有实体目标的技能不移动镜头。
        BattleActionCamera actionCamera = Object.FindAnyObjectByType<BattleActionCamera>();
        bool cameraFocusStarted = actionCamera != null && _actionContext.ResolvedTargets.Count > 0;
        if (cameraFocusStarted)
            yield return actionCamera.FocusOnAction(_actionContext.Actor, _actionContext.ResolvedTargets);
        if (cameraFocusStarted)
            actionCamera.BeginFocusPush();

        // 技能配置了攻击图片时，临时将攻击者放到目标前方并展示该图片。
        // 只处理视觉 Transform，BattleEntity/阵型数据保持不变。
        BattleUnit presentationActor = _actionContext.Actor.Unit;
        // 以第一个有效且非自身的目标确定交战方向；其余 AoE 目标沿用同一位移量，
        // 这样既能一起前移，也能保留原本的相对阵型，不会全部挤到同一个点。
        BattleUnit presentationTarget = _actionContext.ResolvedTargets
            .Select(target => target != null ? target.Unit : null)
            .FirstOrDefault(unit => unit != null && unit != presentationActor);
        bool actionPresentationStarted = false;
        List<BattleUnit> presentedTargets = new List<BattleUnit>();
        Vector3 cameraImpactDirection = Vector3.zero;
        float impactActorX = presentationActor != null
            ? presentationActor.transform.position.x
            : 0f;
        float impactTargetX = presentationTarget != null
            ? presentationTarget.transform.position.x
            : impactActorX + 1f;
        if (presentationActor != null && presentationTarget != null)
        {
            cameraImpactDirection = Vector3.ProjectOnPlane(
                presentationTarget.transform.position - presentationActor.transform.position,
                Vector3.up);
            if (cameraImpactDirection.sqrMagnitude < 0.0001f)
                cameraImpactDirection = presentationActor.transform.forward;
            cameraImpactDirection.Normalize();
        }

        if (_actionContext.Skill.AttackSprite != null &&
            presentationActor != null && presentationTarget != null)
        {
            Vector3 actorPosition = presentationActor.transform.position;
            Vector3 targetPosition = presentationTarget.transform.position;
            Vector3 direction = cameraImpactDirection.sqrMagnitude > 0.0001f
                ? cameraImpactDirection
                : Vector3.ProjectOnPlane(targetPosition - actorPosition, Vector3.up).normalized;

            float actorRadius = 0.25f;
            float targetRadius = 0.25f;
            if (presentationActor.TryGetVisualBounds(out Bounds actorBounds))
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
            Vector3 cameraFrontOffset = actionCamera != null
                ? actionCamera.GetCameraFrontGroundOffset()
                : Vector3.zero;
            // 友方 AoE 不使用近战交战中点，只沿世界 Z 轴整体前移。
            // 保留镜头朝向所决定的前后符号，但不改变任何单位的 X/Y 坐标。
            Vector3 friendlyAoeOffset = new Vector3(0f, 0f, cameraFrontOffset.z);
            Vector3 actorEngagementPosition = isFriendlyAoe
                ? actorPosition + friendlyAoeOffset
                : engagementCenter - direction * (actorRadius + gap * 0.5f) + cameraFrontOffset;
            Vector3 targetEngagementPosition = isFriendlyAoe
                ? targetPosition + friendlyAoeOffset
                : engagementCenter + direction * (targetRadius + gap * 0.5f) + cameraFrontOffset;

            actionPresentationStarted = presentationActor.BeginActionPresentation(
                _actionContext.Skill.AttackSprite, actorEngagementPosition);
            if (actionPresentationStarted)
            {
                // 普通攻击将目标组拉到交战位置；友方 AoE 则让施法者与所有受影响队友
                // 沿同一个世界 Z 轴偏移量整体前移，保持四人的相对站位不变。
                Vector3 targetGroupOffset = isFriendlyAoe
                    ? friendlyAoeOffset
                    : targetEngagementPosition - targetPosition;
                foreach (BattleEntity resolvedTarget in _actionContext.ResolvedTargets)
                {
                    BattleUnit targetUnit = resolvedTarget != null ? resolvedTarget.Unit : null;
                    if (targetUnit == null || targetUnit == presentationActor ||
                        presentedTargets.Contains(targetUnit))
                        continue;

                    Vector3 targetPresentationPosition =
                        targetUnit.transform.position + targetGroupOffset;
                    if (targetUnit.BeginActionPresentation(null, targetPresentationPosition))
                        presentedTargets.Add(targetUnit);
                }

                // 近战攻击图片就是命中帧，因此不在这里等待，下一步立即结算伤害并触发震屏。
            }
        }

        int skillLevel = _actionContext.Actor.RuntimeData.GetSkillLevel(_actionContext.Skill);
        bool movementTriggered;
        bool criticalHit;
        int totalDamage = BattleSkillEffectExecutor.Execute(
            _controller,
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel,
            _actionContext.ResolvedTargets,
            _actionContext.SelectedInputTarget,
            out movementTriggered,
            out criticalHit);

        Debug.Log($"[技能结算] 技能等级：{skillLevel}；本次计算的伤害总值：{totalDamage}；暴击：{criticalHit}。");

        // 当前暴击判定尚未接入；执行器先提供暴击结果出口，未来接入后自动切换更强震屏。
        if (totalDamage > 0 && cameraFocusStarted && actionCamera != null)
            yield return actionCamera.ShakeImpact(criticalHit, impactActorX, impactTargetX);

        // 命中后保留短暂停顿，但不提前恢复攻击者与目标的视觉状态。
        // 攻击图片和目标冻结姿势会一直维持到特写正式结束。
        if (actionPresentationStarted || presentedTargets.Count > 0)
            yield return new WaitForSecondsRealtime(0.12f);

        // 额外特写时间也属于攻击图片展示时间，结束特写前才恢复角色 Animator。
        if (cameraFocusStarted && actionCamera != null &&
            actionCamera.FocusHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(actionCamera.FocusHoldDuration);
        }

        // 伤害飘字的生命周期不能越过攻击特写；镜头复位前统一清理。
        _controller.ClearDamagePopups();

        if (actionPresentationStarted)
            presentationActor.EndActionPresentation();
        foreach (BattleUnit presentedTarget in presentedTargets)
            presentedTarget.EndActionPresentation();

        // 先停止持续推进，再恢复战场全景，最后执行前进/后退等后置位移。
        if (cameraFocusStarted && actionCamera != null)
            actionCamera.EndFocusPush();

        if (cameraFocusStarted && actionCamera != null && actionCamera.isActiveAndEnabled)
            yield return actionCamera.RestoreBattleView();

        // 目前项目尚未建立技能动作播放/完成事件，因此镜头复位完成后再执行后置位移。
        bool postActionMovementTriggered = BattleSkillEffectExecutor.ExecutePostActionMovementEffects(
            _controller,
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel);
        movementTriggered |= postActionMovementTriggered;

        if (movementTriggered)
            yield return new WaitForSecondsRealtime(0.9f);

        if (isEnemyAction && _controller.EnemyActionRecoveryDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionRecoveryDelay);

        if (isEnemyAction && BattleVFXManager.Instance != null)
            BattleVFXManager.Instance.ClearEnemyActionTargets();

        _controller.CompleteAction(_actionContext);
    }
}
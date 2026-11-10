using System.Collections;
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

        // 所有行动者与本次行动的全部实际目标同时进入特写；没有实体目标的技能不移动镜头。
        BattleActionCamera actionCamera = Object.FindAnyObjectByType<BattleActionCamera>();
        bool cameraFocusStarted = actionCamera != null && _actionContext.ResolvedTargets.Count > 0;
        if (cameraFocusStarted)
            yield return actionCamera.FocusOnAction(_actionContext.Actor, _actionContext.ResolvedTargets);

        if (isEnemyAction && _controller.EnemyActionWindupDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionWindupDelay);

        // 技能配置了攻击图片时，临时将攻击者放到目标前方并展示该图片。
        // 只处理视觉 Transform，BattleEntity/阵型数据保持不变。
        BattleUnit presentationActor = _actionContext.Actor.Unit;
        BattleUnit presentationTarget = _actionContext.ResolvedTargets.Count > 0
            ? _actionContext.ResolvedTargets[0]?.Unit
            : null;
        bool actionPresentationStarted = false;
        bool targetPresentationStarted = false;
        Vector3 cameraImpactDirection = Vector3.zero;
        if (presentationActor != null && presentationTarget != null &&
            presentationActor != presentationTarget)
        {
            cameraImpactDirection = Vector3.ProjectOnPlane(
                presentationTarget.transform.position - presentationActor.transform.position,
                Vector3.up);
            if (cameraImpactDirection.sqrMagnitude < 0.0001f)
                cameraImpactDirection = presentationActor.transform.forward;
            cameraImpactDirection.Normalize();
        }

        if (_actionContext.Skill.AttackSprite != null &&
            presentationActor != null && presentationTarget != null &&
            presentationActor != presentationTarget)
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

            // 将两人作为一个整体收拢到原本两者之间的中心点，保留朝向并让双方贴近。
            // 这样镜头里的交战双方会一起被“拎出来”，而不是只把攻击者送到目标脚边。
            Vector3 engagementCenter = (actorPosition + targetPosition) * 0.5f;
            float gap = 0.05f;
            // 两人先保持面对面的相对位置，再整体沿地面朝镜头前移，形成独立的前景交战层。
            Vector3 cameraFrontOffset = actionCamera != null
                ? actionCamera.GetCameraFrontGroundOffset()
                : Vector3.zero;
            Vector3 actorEngagementPosition =
                engagementCenter - direction * (actorRadius + gap * 0.5f) + cameraFrontOffset;
            Vector3 targetEngagementPosition =
                engagementCenter + direction * (targetRadius + gap * 0.5f) + cameraFrontOffset;

            actionPresentationStarted = presentationActor.BeginActionPresentation(
                _actionContext.Skill.AttackSprite, actorEngagementPosition);
            if (actionPresentationStarted)
            {
                targetPresentationStarted = presentationTarget.BeginActionPresentation(
                    null, targetEngagementPosition);
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
            yield return actionCamera.ShakeImpact(criticalHit, cameraImpactDirection);

        if (actionPresentationStarted || targetPresentationStarted)
        {
            yield return new WaitForSecondsRealtime(0.12f);
            if (actionPresentationStarted)
                presentationActor.EndActionPresentation();
            if (targetPresentationStarted)
                presentationTarget.EndActionPresentation();
        }

        // 攻击特写只负责展示攻击与命中。特写结束后先恢复战场全景，
        // 再执行前进/后退等后置位移，避免镜头跟着角色位移阶段继续停留在特写。
        if (cameraFocusStarted && actionCamera != null &&
            actionCamera.FocusHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(actionCamera.FocusHoldDuration);
        }

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
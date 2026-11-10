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
        if (_actionContext.Skill.AttackSprite != null &&
            presentationActor != null && presentationTarget != null &&
            presentationActor != presentationTarget)
        {
            Vector3 actorPosition = presentationActor.transform.position;
            Vector3 targetPosition = presentationTarget.transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(targetPosition - actorPosition, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                direction = presentationActor.transform.forward;
            direction.Normalize();

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
            Vector3 actorEngagementPosition =
                engagementCenter - direction * (actorRadius + gap * 0.5f);
            Vector3 targetEngagementPosition =
                engagementCenter + direction * (targetRadius + gap * 0.5f);

            actionPresentationStarted = presentationActor.BeginActionPresentation(
                _actionContext.Skill.AttackSprite, actorEngagementPosition);
            if (actionPresentationStarted)
            {
                targetPresentationStarted = presentationTarget.BeginActionPresentation(
                    null, targetEngagementPosition);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        int skillLevel = _actionContext.Actor.RuntimeData.GetSkillLevel(_actionContext.Skill);
        bool movementTriggered;
        int totalDamage = BattleSkillEffectExecutor.Execute(
            _controller,
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel,
            _actionContext.ResolvedTargets,
            _actionContext.SelectedInputTarget,
            out movementTriggered);

        Debug.Log($"[技能结算] 技能等级：{skillLevel}；本次计算的伤害总值：{totalDamage}。");

        if (actionPresentationStarted || targetPresentationStarted)
        {
            yield return new WaitForSecondsRealtime(0.12f);
            if (actionPresentationStarted)
                presentationActor.EndActionPresentation();
            if (targetPresentationStarted)
                presentationTarget.EndActionPresentation();
        }

        // 目前项目尚未建立技能动作播放/完成事件，因此即时结算完成后直接进入后置位移阶段。
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
        else if (cameraFocusStarted && actionCamera != null && actionCamera.FocusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(actionCamera.FocusHoldDuration);

        if (cameraFocusStarted && actionCamera != null && actionCamera.isActiveAndEnabled)
            yield return actionCamera.RestoreBattleView();

        if (isEnemyAction && BattleVFXManager.Instance != null)
            BattleVFXManager.Instance.ClearEnemyActionTargets();

        _controller.CompleteAction(_actionContext);
    }
}
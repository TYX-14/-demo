using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 统一执行已确认的玩家或敌方行动，并在结算后交还给 CTB 调度器。
/// 本状态只负责校验、门禁、结算调用与交权；镜头、摆位、震屏、等待等演出时序
/// 全部交给 BattleActionPresentation。
/// </summary>
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

        // 行动已经确认，广播本次技能供 UI 使用。
        // 放在这里而不是选择技能时：取消选择、被拒绝的技能都不会走到这一步。
        EventBus.Publish(new BattleSkillCastEvent(
            _actionContext.Actor,
            _actionContext.Skill,
            _actionContext.ResolvedTargets));

        // 每个行动一个演出实例，行动结束即丢弃，避免跨行动遗留演出状态。
        BattleActionPresentation presentation = new(_controller, _actionContext);

        yield return presentation.PlayApproach();

        int skillLevel = _actionContext.Actor.RuntimeData.GetSkillLevel(_actionContext.Skill);
        bool movementTriggered;
        bool criticalHit;
        bool damageEventTriggered;
        int totalDamage = BattleSkillEffectExecutor.Execute(
            _controller,
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel,
            _actionContext.ResolvedTargets,
            _actionContext.SelectedInputTarget,
            out movementTriggered,
            out criticalHit,
            out damageEventTriggered,
            out List<BattleActionFact> facts);

        Debug.Log($"[技能结算] 技能等级：{skillLevel}；本次计算的伤害总值：{totalDamage}；暴击：{criticalHit}。");

        // 命中帧：广播本次行动的完整事实。此刻特写仍在播放，旁白可以立刻接话。
        if (facts.Count > 0)
            EventBus.Publish(new BattleActionResolvedEvent(_actionContext.Actor, _actionContext.Skill, facts));

        yield return presentation.PlayImpactAndOutro(criticalHit, damageEventTriggered);

        // 攻击特写已经结束、镜头回到全景，仍在当前行动者的回合内。
        // 压力在这里做一次独立结算：合并所有来源后按位置依次跳出，等整轮结束再继续。
        List<BattleStressChange> stressChanges = BattleStressResolver.FromFacts(_controller, facts);
        yield return _controller.PlayStressSettlement(stressChanges);

        // 目前项目尚未建立技能动作播放/完成事件，因此镜头复位完成后再执行后置位移。
        bool postActionMovementTriggered = BattleSkillEffectExecutor.ExecutePostActionMovementEffects(
            _controller,
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel);
        movementTriggered |= postActionMovementTriggered;

        if (movementTriggered)
            yield return new WaitForSecondsRealtime(0.9f);

        // 位移之后再说话：否则角色会一边说话一边滑到新位置。
        List<BattleMoment> moments = new();
        if (BattleMomentResolver.ResolveAll(facts, moments))
        {
            BattleBarkDirector barkDirector = Object.FindAnyObjectByType<BattleBarkDirector>();
            if (barkDirector != null)
                yield return barkDirector.TryPlayBark(_controller, moments);
        }

        yield return presentation.PlayRecovery();

        _controller.CompleteAction(_actionContext);
    }
}

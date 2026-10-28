using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 执行已确认的行动。
/// 当前接入攻击效果结算；其他效果、SP 消耗、命中/暴击判定、动画与行动推进仍待后续阶段实现。
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
            _actionContext.Skill == null ||
            _actionContext.ResolvedTargets == null ||
            (_actionContext.ResolvedTargets.Count == 0 &&
             _actionContext.Skill.TargetSelectionMode != SkillTargetSelectionMode.None))
        {
            Debug.LogWarning("PerformActionState：行动数据不完整或没有有效目标。");
            _controller.CancelPendingAction();
            _controller.SetState(new PlayerInputState(_controller));
            yield break;
        }

        string targetNames = string.Join(", ", _actionContext.ResolvedTargets.Select(
            target => target.Definition != null ? target.Definition.Name : target.ID));

        Debug.Log(
            $"[行动执行] {_actionContext.Actor.Definition.Name} 使用技能「{_actionContext.Skill.SkillName}」；" +
            $"输入位置：{_actionContext.SelectedInputPosition}；实际目标：{targetNames}。");

        int skillLevel = _actionContext.Actor.RuntimeData.GetSkillLevel(_actionContext.Skill);
        int totalDamage = BattleSkillEffectExecutor.Execute(
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel,
            _actionContext.ResolvedTargets);

        Debug.Log(
            $"[技能结算] 技能等级：{skillLevel}；本次已结算的总 HP 伤害：{totalDamage}。" +
            "尚未支持的效果类型会被跳过；本次不扣除 SP，也不推进 CTB 行动顺序。");

        _controller.CancelPendingAction();
        _controller.SetState(new PlayerInputState(_controller));
        yield break;
    }
}

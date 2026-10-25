using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 执行已确认的行动。
/// 当前阶段先验证并记录行动者、技能和解析后的目标，具体伤害、SP 扣除、动画与状态效果留待行动结算阶段接入。
/// 在结算器尚未实现前，不消耗行动或资源，而是返回玩家输入状态，便于验证目标选择链路。
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
            _actionContext.ResolvedTargets.Count == 0)
        {
            Debug.LogWarning("PerformActionState：行动数据不完整或没有有效目标。");
            _controller.CancelPendingAction();
            _controller.SetState(new PlayerInputState(_controller));
            yield break;
        }

        string targetNames = string.Join(", ", _actionContext.ResolvedTargets.Select(
            target => target.Definition != null ? target.Definition.Name : target.ID));

        Debug.Log(
            $"[行动验证] {_actionContext.Actor.Definition.Name} 选择技能「{_actionContext.Skill.SkillName}」；" +
            $"输入位置：{_actionContext.SelectedInputPosition}；实际目标：{targetNames}。");

        Debug.LogWarning(
            "当前仅验证行动数据与目标解析，尚未接入伤害、SP 消耗、动画或状态效果；本次不会消耗行动。");

        _controller.CancelPendingAction();
        _controller.SetState(new PlayerInputState(_controller));
        yield break;
    }
}

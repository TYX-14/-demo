using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 执行已确认的行动。
/// 所有玩家与敌方技能都经过本状态结算；行动开始与结束由 BattleController 统一管理。
/// 当前技能效果仍只支持已接入的攻击伤害与伤害修正。
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

        if (!_controller.TryBeginAction(_actionContext))
        {
            Debug.LogWarning("PerformActionState：行动未能启动，可能已被取消、重复提交或行动者已变化。");
            _controller.RejectUnstartedAction(_actionContext);
            yield break;
        }

        string actorName = _actionContext.Actor.Definition != null
            ? _actionContext.Actor.Definition.Name
            : _actionContext.Actor.ID;
        string targetNames = string.Join(", ", _actionContext.ResolvedTargets.Select(
            target => target != null
                ? (target.Definition != null ? target.Definition.Name : target.ID)
                : "无效目标"));

        Debug.Log(
            $"[行动开始] {actorName} 使用技能「{_actionContext.Skill.SkillName}」；" +
            $"输入位置：{_actionContext.SelectedInputPosition}；实际目标：{targetNames}。");

        // 当前尚未接入敌方攻击动画，先用可配置的短前摇让玩家看清敌人即将行动。
        if (!_actionContext.Actor.IsPlayer && _controller.EnemyActionWindupDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionWindupDelay);

        int skillLevel = _actionContext.Actor.RuntimeData.GetSkillLevel(_actionContext.Skill);
        int totalDamage = BattleSkillEffectExecutor.Execute(
            _actionContext.Actor,
            _actionContext.Skill,
            skillLevel,
            _actionContext.ResolvedTargets);

        Debug.Log(
            $"[技能结算] 技能等级：{skillLevel}；本次计算的伤害总值：{totalDamage}。" +
            "尚未接入的效果类型会被跳过；当前尚未处理 SP 消耗、命中/暴击判定与动画。");

        // 给敌方结算结果留出可见时间，再推进 CTB，避免伤害日志与行动头像瞬间跳过。
        if (!_actionContext.Actor.IsPlayer && _controller.EnemyActionRecoveryDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyActionRecoveryDelay);

        // 由控制器统一清理行动上下文并推进到下一行动者，避免在此直接切回玩家输入状态。
        _controller.CompleteAction(_actionContext);
        yield break;
    }
}
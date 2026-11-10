using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小可运行的敌方行动选择。
/// 当前按装备技能列表顺序选择第一个站位与目标均合法的技能，并选择第一个合法目标；
/// 后续可替换为更完整的 AI 策略，但行动结算仍统一进入 PerformActionState。
/// </summary>
public class EnemyAIState : BattleState
{
    public EnemyAIState(BattleController controller) : base(controller)
    {
    }

    public override IEnumerator Execute()
    {
        BattleEntity actor = _controller.CurrentEntity;
        if (actor == null || actor.IsPlayer || !actor.IsAlive || actor.RuntimeData == null)
        {
            _controller.SkipCurrentTurn(actor);
            yield break;
        }

        // AI 状态可以先被状态机选中，但必须等 CTB 大头像完成入场后才真正开始行动。
        // BattleController 会缓存展示完成的行动者，避免头像动画极短或同步事件先到时漏接事件。
        while (_controller.IsBattleRunning &&
               _controller.CurrentEntity == actor &&
               _controller.CurrentState == this &&
               !_controller.IsCTBDisplayReadyFor(actor))
        {
            yield return null;
        }

        if (!_controller.IsBattleRunning ||
            _controller.CurrentEntity != actor ||
            _controller.CurrentState != this)
            yield break;

        if (!actor.IsAlive)
        {
            _controller.SkipCurrentTurn(actor);
            yield break;
        }

        // 蓝色行动圈由 BattleVFXManager 在同一个 CTB 展示完成事件中点亮。
        // 使用真实时间等待，让暂停/时间缩放不会意外改变敌方思考时长。
        if (_controller.EnemyDecisionDelay > 0f)
            yield return new WaitForSecondsRealtime(_controller.EnemyDecisionDelay);

        // 等待期间若战斗结束、行动者变化或状态被替换，不再提交过期决策。
        if (!_controller.IsBattleRunning ||
            _controller.CurrentEntity != actor ||
            _controller.CurrentState != this)
            yield break;

        if (!actor.IsAlive)
        {
            _controller.SkipCurrentTurn(actor);
            yield break;
        }

        IReadOnlyList<SkillDefinitionSO> skills = actor.RuntimeData.EquippedSkills;
        if (skills != null)
        {
            foreach (SkillDefinitionSO skill in skills)
            {
                if (skill == null ||
                    !BattleTargeting.CanUseFromPosition(skill, _controller.GetLogicalPosition(actor)))
                    continue;

                BattleActionContext actionContext = new BattleActionContext(actor, skill);
                bool resolved = false;

                if (BattleTargeting.RequiresManualSelection(skill))
                {
                    List<BattleEntity> candidates = BattleTargeting.GetLegalTargets(
                        skill, actor, _controller.AllEntities, _controller.GetLogicalPosition);

                    foreach (BattleEntity candidate in candidates)
                    {
                        if (!BattleTargeting.TryResolveSelectedTarget(
                                skill,
                                actor,
                                candidate,
                                _controller.AllEntities,
                                _controller.GetLogicalPosition,
                                out int selectedPosition,
                                out List<BattleEntity> resolvedTargets))
                            continue;

                        actionContext.SetResolvedTargets(candidate, selectedPosition, resolvedTargets);
                        resolved = true;
                        break;
                    }
                }
                else
                {
                    resolved = BattleTargeting.TryResolveAutomaticTargets(
                        skill,
                        actor,
                        _controller.AllEntities,
                        _controller.GetLogicalPosition,
                        out BattleEntity selectedTarget,
                        out int selectedPosition,
                        out List<BattleEntity> resolvedTargets);

                    if (resolved)
                        actionContext.SetResolvedTargets(selectedTarget, selectedPosition, resolvedTargets);
                }

                if (!resolved)
                    continue;

                if (_controller.SubmitAIAction(actionContext))
                {
                    Debug.Log($"[敌方 AI] {actor.Definition.Name} 选择技能「{skill.SkillName}」。", actor.Unit);
                    yield break;
                }
            }
        }

        // 暂时没有更复杂的普通攻击/防御决策。没有可用技能时跳过本次行动，避免状态机永久等待。
        _controller.SkipCurrentTurn(actor);
        yield break;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 结算技能中的即时效果。持续状态类效果暂不在此执行；所有即时效果共享同一行动上下文。
/// </summary>
public static class BattleSkillEffectExecutor
{
    public static int Execute(
        BattleController controller,
        BattleEntity actor,
        SkillDefinitionSO skill,
        int skillLevel,
        IReadOnlyList<BattleEntity> targets,
        BattleEntity selectedInputTarget,
        out bool movementTriggered)
    {
        movementTriggered = false;
        if (controller == null || actor == null || actor.RuntimeData == null ||
            skill == null || targets == null || !actor.IsAlive)
            return 0;

        SkillLevelConfig levelConfig = skill.GetLevelConfig(skillLevel);
        if (levelConfig == null || levelConfig.Effects == null)
            return 0;

        int totalCalculatedDamage = 0;
        IReadOnlyList<SkillEffectBase> effects = levelConfig.Effects;

        // 换位是行动者与被选中的一个目标之间的关系操作，不对 AoE 目标列表逐个执行。
        // 前进/后退则属于行动者自身的位移，也只执行一次，与技能选择了谁作为目标无关。
        foreach (SkillEffectBase effect in effects)
        {
            if (effect is SwapPositionEffect)
            {
                if (selectedInputTarget != null &&
                    controller.FieldManager != null &&
                    controller.FieldManager.TrySwapEntities(actor, selectedInputTarget))
                    movementTriggered = true;
            }
            else if (effect is MoveForwardEffect moveForward)
            {
                int distance = Mathf.Abs(Mathf.RoundToInt(moveForward.Distance));
                if (distance > 0 && controller.FieldManager != null &&
                    controller.FieldManager.TryMoveEntityBySlots(actor, -distance))
                    movementTriggered = true;
            }
            else if (effect is MoveBackwardEffect moveBackward)
            {
                int distance = Mathf.Abs(Mathf.RoundToInt(moveBackward.Distance));
                if (distance > 0 && controller.FieldManager != null &&
                    controller.FieldManager.TryMoveEntityBySlots(actor, distance))
                    movementTriggered = true;
            }
        }

        foreach (BattleEntity target in targets)
        {
            if (target == null || target.RuntimeData == null || !target.IsAlive)
                continue;

            float damageModifier = GetDamageModifier(effects, target);

            foreach (SkillEffectBase effect in effects)
            {
                if (effect == null)
                    continue;

                if (effect is DamageEffect)
                {
                    int damage = BattleDamageCalculator.CalculateDamage(
                        actor.RuntimeData, target.RuntimeData, skill.DamageType,
                        damageModifier, isCritical: false);

                    // 零伤害不调用 ModifyHp，避免误触发死门判定。
                    if (damage > 0)
                    {
                        target.RuntimeData.ModifyHp(-damage);
                        totalCalculatedDamage += damage;
                        target.Unit?.UpdateVisuals();
                    }

                    if (!target.IsAlive)
                        break;
                }
                else if (effect is HealEffect heal)
                {
                    target.RuntimeData.ModifyHp(Mathf.Abs(Mathf.RoundToInt(heal.Value)));
                    target.Unit?.UpdateVisuals();
                }
                else if (effect is StressDamageEffect stressDamage)
                {
                    target.RuntimeData.ModifyStress(Mathf.Abs(Mathf.RoundToInt(stressDamage.Value)));
                }
                else if (effect is StressHealEffect stressHeal)
                {
                    target.RuntimeData.ModifyStress(-Mathf.Abs(Mathf.RoundToInt(stressHeal.Value)));
                }
                else if (effect is MoveForwardEffect || effect is MoveBackwardEffect)
                {
                    // 前进/后退已在上方对行动者执行，不应随着目标数量重复触发。
                }
                else if (effect is KnockbackEffect knockback)
                {
                    if (controller.FieldManager != null &&
                        controller.FieldManager.TryMoveEntityBySlots(target, knockback.Distance, true))
                        movementTriggered = true;
                }
                else if (effect is StunEffect stun)
                {
                    target.RuntimeData.StunActionsRemaining = Mathf.Max(
                        target.RuntimeData.StunActionsRemaining, stun.Actions);
                    target.Unit?.RefreshStunVisual();
                }
                // ApplyStatusEffect 与命中/暴击修正不在本阶段执行。
            }
        }

        return totalCalculatedDamage;
    }

    private static float GetDamageModifier(IReadOnlyList<SkillEffectBase> effects, BattleEntity target)
    {
        float modifier = 0f;
        foreach (SkillEffectBase effect in effects)
        {
            if (effect is DamageModifierEffect damageModifierEffect &&
                MatchesCondition(damageModifierEffect.Condition, target))
                modifier += damageModifierEffect.Percent;
        }

        return modifier;
    }

    private static bool MatchesCondition(SkillEffectCondition condition, BattleEntity target)
    {
        switch (condition)
        {
            case SkillEffectCondition.None:
                return true;
            case SkillEffectCondition.TargetSizeGreaterThanOne:
                return target != null &&
                       target.Definition is EnemyDfinitionSo enemyDefinition &&
                       enemyDefinition.occupiedSlotCount > 1;
            default:
                return false;
        }
    }
}
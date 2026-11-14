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
        out bool movementTriggered,
        out bool criticalHit,
        out bool damageEventTriggered,
        out List<BattleActionFact> facts)
    {
        movementTriggered = false;
        criticalHit = false;
        damageEventTriggered = false;
        facts = new List<BattleActionFact>();
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
            // 前进/后退统一在技能即时效果结算完成后执行，避免打断技能动作时序。
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
                    bool wasAlive = target.IsAlive;
                    int hpBefore = target.CurrentHP;

                    // 每个目标独立掷一次暴击。
                    bool isCritical = RollCritical(actor, effects, target);
                    criticalHit |= isCritical;

                    int damage = BattleDamageSystem.ApplyDamage(
                        actor,
                        target,
                        skill.DamageType,
                        out bool eventPublished,
                        damageModifier,
                        isCritical);

                    totalCalculatedDamage += damage;
                    damageEventTriggered |= eventPublished;

                    facts.Add(new BattleActionFact(
                        actor, target, BattleFactKind.Damage, damage, isCritical,
                        hpBefore, target.CurrentHP, wasAlive && !target.IsAlive));

                    if (!target.IsAlive)
                        break;
                }
                else if (effect is HealEffect heal)
                {
                    int hpBefore = target.CurrentHP;
                    bool isCritical = RollCritical(actor, effects, target);

                    int amount = Mathf.Abs(Mathf.RoundToInt(heal.Value));
                    if (isCritical)
                        amount *= 2;

                    target.RuntimeData.ModifyHp(amount);
                    target.Unit?.UpdateVisuals();

                    facts.Add(new BattleActionFact(
                        actor, target, BattleFactKind.Heal, target.CurrentHP - hpBefore, isCritical,
                        hpBefore, target.CurrentHP));
                }
                else if (effect is StressDamageEffect stressDamage)
                {
                    int amount = Mathf.Abs(Mathf.RoundToInt(stressDamage.Value));
                    // 只记录事实，实际写入交给 BattleStressResolver 统一合并。
                    facts.Add(new BattleActionFact(
                        actor, target, BattleFactKind.StressDamage, amount, false,
                        target.CurrentHP, target.CurrentHP));
                }
                else if (effect is StressHealEffect stressHeal)
                {
                    int amount = Mathf.Abs(Mathf.RoundToInt(stressHeal.Value));
                    facts.Add(new BattleActionFact(
                        actor, target, BattleFactKind.StressHeal, amount, false,
                        target.CurrentHP, target.CurrentHP));
                }
                else if (effect is MoveForwardEffect || effect is MoveBackwardEffect)
                {
                    // 前进/后退已在上方对行动者执行，不应随着目标数量重复触发。
                }
                else if (effect is KnockbackEffect knockback)
                {
                    if (controller.FieldManager != null &&
                        controller.FieldManager.TryMoveEntityBySlots(target, knockback.Distance, true))
                    {
                        movementTriggered = true;

                        facts.Add(new BattleActionFact(
                            actor, target, BattleFactKind.Knockback, knockback.Distance, false,
                            target.CurrentHP, target.CurrentHP));
                    }
                }
                else if (effect is StunEffect stun)
                {
                    target.RuntimeData.StunActionsRemaining = Mathf.Max(
                        target.RuntimeData.StunActionsRemaining, stun.Actions);
                    target.Unit?.RefreshStunVisual();

                    facts.Add(new BattleActionFact(
                        actor, target, BattleFactKind.Stun, stun.Actions, false,
                        target.CurrentHP, target.CurrentHP));
                }
                // ApplyStatusEffect 与命中/暴击修正不在本阶段执行。
            }
        }

        return totalCalculatedDamage;
    }

    /// <summary>
    /// 在技能本身的动作/即时结算完成后执行行动者的前进与后退。
    /// 当前尚无技能动作播放系统；PerformActionState 中的调用点就是未来等待动作完成信号的位置。
    /// </summary>
    public static bool ExecutePostActionMovementEffects(
        BattleController controller,
        BattleEntity actor,
        SkillDefinitionSO skill,
        int skillLevel)
    {
        if (controller == null || actor == null || actor.RuntimeData == null ||
            skill == null || !actor.IsAlive)
            return false;

        SkillLevelConfig levelConfig = skill.GetLevelConfig(skillLevel);
        if (levelConfig == null || levelConfig.Effects == null || controller.FieldManager == null)
            return false;

        bool movementTriggered = false;
        foreach (SkillEffectBase effect in levelConfig.Effects)
        {
            if (effect is MoveForwardEffect moveForward)
            {
                int distance = Mathf.Abs(Mathf.RoundToInt(moveForward.Distance));
                if (distance > 0 &&
                    controller.FieldManager.TryMoveEntityBySlots(actor, -distance, false, 0.12f, true))
                    movementTriggered = true;
            }
            else if (effect is MoveBackwardEffect moveBackward)
            {
                int distance = Mathf.Abs(Mathf.RoundToInt(moveBackward.Distance));
                if (distance > 0 &&
                    controller.FieldManager.TryMoveEntityBySlots(actor, distance, false, 0.12f, true))
                    movementTriggered = true;
            }
        }

        return movementTriggered;
    }

    /// <summary>
    /// 为单个目标掷一次暴击：角色暴击率 + 技能上的暴击修正，百分比制。
    /// 每个目标独立判定，所以 AoE 可能出现部分暴击。
    /// </summary>
    private static bool RollCritical(
        BattleEntity actor,
        IReadOnlyList<SkillEffectBase> effects,
        BattleEntity target)
    {
        if (actor == null || actor.RuntimeData == null)
            return false;

        float chance = actor.RuntimeData.CritChancePercent + GetCritChanceModifier(effects, target);
        if (chance <= 0f)
            return false;

        return Random.value * 100f < Mathf.Min(chance, 100f);
    }

    /// <summary>汇总本次技能里对目标生效的暴击修正（百分比）。</summary>
    private static float GetCritChanceModifier(
        IReadOnlyList<SkillEffectBase> effects,
        BattleEntity target)
    {
        float modifier = 0f;

        foreach (SkillEffectBase effect in effects)
        {
            if (effect is CriticalModifierEffect critical &&
                MatchesCondition(critical.Condition, target))
            {
                modifier += critical.Percent;
            }
        }

        return modifier;
    }

    /// <summary>
    /// 预览技能对某个目标的伤害区间（不含暴击，含防御减免与 ±20% 浮动）。
    /// 返回 false 表示这个技能没有伤害效果。
    /// </summary>
    public static bool TryGetDamageRange(
        BattleEntity actor,
        BattleEntity target,
        SkillDefinitionSO skill,
        int skillLevel,
        out int minDamage,
        out int maxDamage)
    {
        minDamage = 0;
        maxDamage = 0;

        if (actor?.RuntimeData == null || target?.RuntimeData == null || skill == null)
            return false;

        SkillLevelConfig levelConfig = skill.GetLevelConfig(skillLevel);
        if (levelConfig?.Effects == null)
            return false;

        bool hasDamage = false;
        foreach (SkillEffectBase effect in levelConfig.Effects)
        {
            if (effect is DamageEffect)
            {
                hasDamage = true;
                break;
            }
        }

        if (!hasDamage)
            return false;

        float modifier = GetDamageModifier(levelConfig.Effects, target);
        StatBlock attackerStats = actor.RuntimeData.GetTotalStats();
        StatBlock targetStats = target.RuntimeData.GetTotalStats();

        minDamage = BattleDamageCalculator.CalculateDamageWithRoll(
            attackerStats, targetStats, skill.DamageType,
            1f - BattleDamageCalculator.DamageVariance, modifier, false);

        maxDamage = BattleDamageCalculator.CalculateDamageWithRoll(
            attackerStats, targetStats, skill.DamageType,
            1f + BattleDamageCalculator.DamageVariance, modifier, false);

        return true;
    }

    /// <summary>技能是否带有控制效果（当前只有眩晕）。</summary>
    public static bool HasControlEffect(SkillDefinitionSO skill, int skillLevel)
    {
        if (skill == null)
            return false;

        SkillLevelConfig levelConfig = skill.GetLevelConfig(skillLevel);
        if (levelConfig?.Effects == null)
            return false;

        foreach (SkillEffectBase effect in levelConfig.Effects)
        {
            if (effect is StunEffect)
                return true;
        }

        return false;
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
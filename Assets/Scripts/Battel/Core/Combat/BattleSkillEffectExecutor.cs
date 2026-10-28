using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 执行技能等级中已支持的即时效果。
/// 当前只结算 Damage 与 DamageModifier；其他效果类型留给后续阶段实现。
/// 不负责命中、暴击概率、SP 消耗、行动推进或动画。
/// </summary>
public static class BattleSkillEffectExecutor
{
    /// <summary>
    /// 执行技能的攻击效果。每个 Damage 项代表一次攻击；
    /// 本次攻击的 DamageModifier 会先按目标条件汇总，再传入伤害计算器。
    /// </summary>
    public static int Execute(
        BattleEntity actor,
        SkillDefinitionSO skill,
        int skillLevel,
        IReadOnlyList<BattleEntity> targets)
    {
        if (actor == null || actor.RuntimeData == null ||
            skill == null || targets == null || !actor.IsAlive)
        {
            return 0;
        }

        SkillLevelConfig levelConfig = skill.GetLevelConfig(skillLevel);
        if (levelConfig == null || levelConfig.Effects == null)
            return 0;

        int totalCalculatedDamage = 0;

        foreach (BattleEntity target in targets)
        {
            if (target == null || target.RuntimeData == null || !target.IsAlive)
                continue;

            float damageModifier = GetDamageModifier(levelConfig.Effects, target);
            foreach (SkillEffectBase effect in levelConfig.Effects)
            {
                if (!(effect is DamageEffect))
                    continue;

                int damage = BattleDamageCalculator.CalculateDamage(
                    actor.RuntimeData,
                    target.RuntimeData,
                    skill.DamageType,
                    damageModifier,
                    isCritical: false);

                // 0 伤害不能调用 ModifyHp，否则目标处于死门时会错误地触发致死判定。
                if (damage <= 0)
                    continue;

                target.RuntimeData.ModifyHp(-damage);
                totalCalculatedDamage += damage;

                if (target.Unit != null)
                    target.Unit.UpdateVisuals();

                // ModifyHp 可能通过死门判定改变死亡状态。死亡后不再结算该目标的后续多段攻击。
                if (!target.IsAlive)
                    break;
            }
        }

        return totalCalculatedDamage;
    }

    private static float GetDamageModifier(
        IReadOnlyList<SkillEffectBase> effects,
        BattleEntity target)
    {
        float modifier = 0f;
        foreach (SkillEffectBase effect in effects)
        {
            DamageModifierEffect damageModifierEffect = effect as DamageModifierEffect;
            if (damageModifierEffect == null)
                continue;

            if (MatchesCondition(damageModifierEffect.Condition, target))
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

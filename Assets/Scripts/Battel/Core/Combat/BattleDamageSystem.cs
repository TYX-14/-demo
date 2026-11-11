using MFrameork.Event;

/// <summary>
/// 战斗伤害结算入口。
/// 负责计算伤害、修改目标生命值，并在伤害结算后发布事件。
/// 不负责飘字、镜头、音效等表现。
/// </summary>
public static class BattleDamageSystem
{
    /// <summary>
    /// 对目标造成一次伤害，返回本次计算出的伤害数值。
    /// 只要攻击有效命中，就发布伤害事件；即使最终伤害为 0，也代表目标确实受到了攻击。
    /// </summary>
    public static int ApplyDamage(
        BattleEntity attacker,
        BattleEntity target,
        DamageType damageType,
        float damageModifierPercent = 0f,
        bool isCritical = false,
        out bool damageEventPublished)
    {
        damageEventPublished = false;

        if (attacker == null || attacker.RuntimeData == null ||
            target == null || target.RuntimeData == null ||
            !attacker.IsAlive || !target.IsAlive)
            return 0;

        int damage = BattleDamageCalculator.CalculateDamage(
            attacker.RuntimeData,
            target.RuntimeData,
            damageType,
            damageModifierPercent,
            isCritical);

        // 只有正数伤害才扣除生命值；0 点伤害仍然属于一次有效命中。
        if (damage > 0)
            target.RuntimeData.ModifyHp(-damage);

        EventBus.Publish(new BattleDamageEvent(
            attacker,
            target,
            damage,
            damageType,
            isCritical));
        damageEventPublished = true;

        return damage;
    }
}
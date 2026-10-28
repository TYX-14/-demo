using UnityEngine;

/// <summary>
/// 本次攻击采用的伤害属性类型。
/// </summary>
public enum DamageType
{
    Physical,
    Magical
}

/// <summary>
/// 战斗伤害的基础计算器。
/// 只计算一次伤害的数值，不负责命中判定、暴击概率、状态触发、HP 扣除或行动推进。
/// </summary>
public static class BattleDamageCalculator
{
    /// <summary>
    /// 防御曲线参数 K。减伤率 = 防御 / (防御 + K)。
    /// 当前角色属性大多在个位数到数十的量级，先使用 20，避免低防御过度减伤。
    /// 后续应结合实际战斗测试调整。
    /// </summary>
    public const float DefenseScaleK = 20f;

    /// <summary>
    /// 伤害浮动范围为基础值的 ±20%。
    /// </summary>
    public const float DamageVariance = 0.2f;

    /// <summary>
    /// 暴击造成双倍伤害。
    /// </summary>
    public const float CriticalMultiplier = 2f;

    /// <summary>
    /// 生成一次带随机浮动的伤害。物理攻击对抗物理防御，魔法攻击对抗魔法防御。
    /// damageModifierPercent 是技能对本次攻击的最终伤害修正百分数，例如 10 表示 +10%，-50 表示 -50%。
    /// </summary>
    public static int CalculateDamage(
        CharacterRuntimeData attacker,
        CharacterRuntimeData target,
        DamageType damageType,
        float damageModifierPercent = 0f,
        bool isCritical = false)
    {
        if (attacker == null || target == null || target.IsDead)
            return 0;

        float variance = Random.Range(1f - DamageVariance, 1f + DamageVariance);
        return CalculateDamageWithRoll(
            attacker.GetTotalStats(),
            target.GetTotalStats(),
            damageType,
            variance,
            damageModifierPercent,
            isCritical);
    }

    /// <summary>
    /// 使用调用方提供的浮动倍率进行确定性计算，便于测试和复现战斗数值。
    /// varianceMultiplier 通常位于 0.8 到 1.2；本方法会将其限制在该范围。
    /// </summary>
    public static int CalculateDamageWithRoll(
        StatBlock attackerStats,
        StatBlock targetStats,
        DamageType damageType,
        float varianceMultiplier,
        float damageModifierPercent = 0f,
        bool isCritical = false)
    {
        int attack = damageType == DamageType.Magical
            ? attackerStats.MAtk
            : attackerStats.PAtk;

        int defense = damageType == DamageType.Magical
            ? targetStats.MDef
            : targetStats.PDef;

        if (attack <= 0)
            return 0;

        defense = Mathf.Max(0, defense);
        float defenseReduction = defense / (defense + DefenseScaleK);
        float damageAfterDefense = attack * (1f - defenseReduction);
        float variance = Mathf.Clamp(
            varianceMultiplier,
            1f - DamageVariance,
            1f + DamageVariance);
        float skillModifier = Mathf.Max(0f, 1f + damageModifierPercent / 100f);

        float damage = damageAfterDefense * variance * skillModifier;
        if (isCritical)
            damage *= CriticalMultiplier;

        // 允许伤害为 0，不强制造成至少 1 点伤害。
        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }
}

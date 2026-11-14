using System.Collections.Generic;

/// <summary>一次行动可以被旁白解读成的“瞬间”。</summary>
public enum BattleMomentKind
{
    None,
    AllMissed,
    AllyKilled,
    EnemyKilled,
    MultiKill,
    LowHpSurvive,
    AllyCriticallyHit,
    CriticalHit,
    Healed,
    Stunned,
    KnockedBack,

    /// <summary>普通命中：没有任何特殊事件，但有实际伤害。优先级最低。</summary>
    Hit
}

/// <summary>解析出来的一个瞬间：类型 + 相关人员。</summary>
public readonly struct BattleMoment
{
    public BattleMomentKind Kind { get; }
    public BattleEntity Actor { get; }
    public BattleEntity Target { get; }

    public BattleMoment(BattleMomentKind kind, BattleEntity actor, BattleEntity target)
    {
        Kind = kind;
        Actor = actor;
        Target = target;
    }
}

/// <summary>
/// 把一次行动的事实清单归纳成若干瞬间，按优先级从高到低排列。
/// 同时发生多个事件（例如暴击击杀）时会全部收集，由消费者决定用哪一个。
/// </summary>
public static class BattleMomentResolver
{
    public const float LowHpThreshold = 0.2f;

    public static bool ResolveAll(IReadOnlyList<BattleActionFact> facts, List<BattleMoment> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (facts == null || facts.Count == 0)
            return false;

        int playerDeaths = 0;
        int enemyDeaths = 0;
        int misses = 0;
        bool allyCriticallyHit = false;
        bool anyPlayerCritical = false;
        bool anyHeal = false;
        bool anyStun = false;
        bool anyKnockback = false;
        bool lowHpSurvive = false;
        bool anyDamage = false;
        BattleActionFact firstDamage = default;

        BattleActionFact firstPlayerDeath = default;
        BattleActionFact firstEnemyDeath = default;
        BattleActionFact bestCritical = default;

        for (int i = 0; i < facts.Count; i++)
        {
            BattleActionFact fact = facts[i];

            switch (fact.Kind)
            {
                case BattleFactKind.Damage:
                    anyDamage = true;
                    if (firstDamage.Target == null)
                        firstDamage = fact;

                    if (fact.IsKillingBlow)
                    {
                        if (fact.TargetIsPlayer)
                        {
                            if (playerDeaths == 0)
                                firstPlayerDeath = fact;
                            playerDeaths++;
                        }
                        else
                        {
                            if (enemyDeaths == 0)
                                firstEnemyDeath = fact;
                            enemyDeaths++;
                        }
                    }

                    if (fact.IsCritical)
                    {
                        if (fact.TargetIsPlayer)
                        {
                            allyCriticallyHit = true;
                        }
                        else if (fact.Actor != null && fact.Actor.IsPlayer)
                        {
                            // 只有我方打出的暴击才算 CriticalHit，敌方暴击我方只记 AllyCriticallyHit。
                            anyPlayerCritical = true;
                            if (bestCritical.Target == null)
                                bestCritical = fact;
                        }
                    }

                    if (!fact.IsKillingBlow && fact.HpAfter > 0 && fact.Target != null)
                    {
                        int maxHp = fact.Target.TotalStats.MaxHP;
                        if (maxHp > 0 && fact.HpAfter <= maxHp * LowHpThreshold)
                            lowHpSurvive = true;
                    }

                    break;

                case BattleFactKind.Heal:
                    anyHeal = true;
                    break;

                case BattleFactKind.Stun:
                    anyStun = true;
                    break;

                case BattleFactKind.Knockback:
                    anyKnockback = true;
                    break;

                case BattleFactKind.Miss:
                    misses++;
                    break;
            }
        }

        BattleEntity actor = facts[0].Actor;

        // 按优先级从高到低依次收集：同时发生多个事件时，消费者可以逐级降级尝试。
        if (playerDeaths > 0)
            results.Add(new BattleMoment(BattleMomentKind.AllyKilled, actor, firstPlayerDeath.Target));

        if (enemyDeaths > 1)
            results.Add(new BattleMoment(BattleMomentKind.MultiKill, actor, firstEnemyDeath.Target));

        if (enemyDeaths >= 1)
            results.Add(new BattleMoment(BattleMomentKind.EnemyKilled, actor, firstEnemyDeath.Target));

        if (allyCriticallyHit)
            results.Add(new BattleMoment(BattleMomentKind.AllyCriticallyHit, actor, null));

        if (anyPlayerCritical)
            results.Add(new BattleMoment(BattleMomentKind.CriticalHit, actor, bestCritical.Target));

        if (misses == facts.Count && misses > 0)
            results.Add(new BattleMoment(BattleMomentKind.AllMissed, actor, null));

        if (lowHpSurvive)
            results.Add(new BattleMoment(BattleMomentKind.LowHpSurvive, actor, null));

        if (anyHeal)
            results.Add(new BattleMoment(BattleMomentKind.Healed, actor, null));

        if (anyStun)
            results.Add(new BattleMoment(BattleMomentKind.Stunned, actor, null));

        if (anyKnockback)
            results.Add(new BattleMoment(BattleMomentKind.KnockedBack, actor, null));

        // 兜底：没有任何特殊事件但有实际伤害时，才算普通命中。
        if (results.Count == 0 && anyDamage)
            results.Add(new BattleMoment(BattleMomentKind.Hit, actor, firstDamage.Target));

        return results.Count > 0;
    }

    /// <summary>只取优先级最高的那一个事件（旁白用）。</summary>
    public static bool TryResolve(IReadOnlyList<BattleActionFact> facts, out BattleMoment moment)
    {
        moment = default;

        List<BattleMoment> results = new();
        if (!ResolveAll(facts, results))
            return false;

        moment = results[0];
        return true;
    }
}

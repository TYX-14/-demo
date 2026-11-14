using System.Collections.Generic;
using UnityEngine;

/// <summary>结算后某个角色的压力总变化。正数增压，负数减压。</summary>
public readonly struct BattleStressChange
{
    public BattleEntity Entity { get; }
    public int Delta { get; }

    /// <summary>是否为本次行动的直接受害者（被暴击者），结算时排在最前。</summary>
    public bool IsPrimary { get; }

    public BattleStressChange(BattleEntity entity, int delta, bool isPrimary = false)
    {
        Entity = entity;
        Delta = delta;
        IsPrimary = isPrimary;
    }
}

/// <summary>一次压力结算中，某个角色最终的处理结果。</summary>
public readonly struct BattleStressOutcome
{
    public BattleEntity Entity { get; }

    /// <summary>实际写入的变化量。</summary>
    public int Applied { get; }

    /// <summary>表现层要显示的数值。美德角色数值不变，但这里仍是计划值。</summary>
    public int Display { get; }

    public BattleStressOutcome(BattleEntity entity, int applied, int display)
    {
        Entity = entity;
        Applied = applied;
        Display = display;
    }
}

/// <summary>
/// 把一次行动里的所有压力来源汇总成「每个角色一个总变化量」，再统一写入。
///
/// 来源：
/// - 技能自带的压力伤害 / 压力回复（来自 facts）
/// - 我方角色被暴击：本人拿最大值，左右相邻的存活队友拿较小值
/// - 我方打出暴击：本人与左右邻居减压
/// - 治疗暴击：全队减压
/// - 队友说话：全队增压或减压（FromBark）
///
/// 所有来源都产出同样的 BattleStressChange，再走同一条 Apply 写入路径。
/// </summary>
public static class BattleStressResolver
{
    // 未配置 BattleStressConfig 时使用的默认值，与 Inspector 默认一致。
    private const int DefaultVictimMin = 6;
    private const int DefaultVictimMax = 8;
    private const int DefaultNeighborMin = 3;
    private const int DefaultNeighborMax = 4;
    private const int DefaultHealCriticalRecovery = 4;
    private const int DefaultCritRecoverySelf = 3;
    private const int DefaultCritRecoveryNeighbor = 2;

    /// <summary>
    /// 计算本次行动的全部压力变化，按位置升序返回，但不写入任何数据。
    /// 真正的写入由 Apply 逐个完成，这样每个角色结算的瞬间就能触发一次通知。
    /// </summary>
    public static List<BattleStressChange> FromFacts(
        BattleController controller,
        IReadOnlyList<BattleActionFact> facts)
    {
        List<BattleStressChange> result = new();

        if (controller == null || facts == null || facts.Count == 0)
            return result;

        BattleStressConfig config = controller.StressConfig;

        Dictionary<BattleEntity, int> deltas = new();
        HashSet<BattleEntity> primary = new();

        // ① 技能自带的压力效果
        for (int i = 0; i < facts.Count; i++)
        {
            BattleActionFact fact = facts[i];
            if (fact.Target == null)
                continue;

            switch (fact.Kind)
            {
                case BattleFactKind.StressDamage:
                    Add(deltas, fact.Target, fact.Amount);
                    break;

                case BattleFactKind.StressHeal:
                    Add(deltas, fact.Target, -fact.Amount);
                    break;
            }
        }

        // ② 我方被暴击：本人 + 左右邻居
        for (int i = 0; i < facts.Count; i++)
        {
            BattleActionFact fact = facts[i];
            if (fact.Kind != BattleFactKind.Damage || !fact.IsCritical || !fact.TargetIsPlayer)
                continue;

            BattleEntity victim = fact.Target;
            if (victim == null)
                continue;

            // 被暴击者是本次结算的直接受害者，优先进表现列表。
            primary.Add(victim);

            Add(deltas, victim, Roll(config, true));

            foreach (BattleEntity neighbor in GetNeighbors(controller, victim))
                Add(deltas, neighbor, Roll(config, false));
        }

        // ②b 我方打出暴击：本人与左右邻居恢复压力，与被暴击波及完全对称，方向相反
        HashSet<BattleEntity> criticalActors = new();

        for (int i = 0; i < facts.Count; i++)
        {
            BattleActionFact fact = facts[i];
            if (fact.Kind != BattleFactKind.Damage || !fact.IsCritical || fact.TargetIsPlayer)
                continue;

            BattleEntity attacker = fact.Actor;
            if (attacker == null || !attacker.IsPlayer || !attacker.IsAlive)
                continue;

            // 同一角色在一次行动里多次暴击只算一次，避免 AoE 全暴击时刷爆压力。
            if (!criticalActors.Add(attacker))
                continue;

            // 打出暴击的人和对被暴击者的处理一致：优先进表现列表。
            primary.Add(attacker);

            int selfRecovery = config != null
                ? Mathf.Max(0, config.critRecoverySelf)
                : DefaultCritRecoverySelf;
            int neighborRecovery = config != null
                ? Mathf.Max(0, config.critRecoveryNeighbor)
                : DefaultCritRecoveryNeighbor;

            Add(deltas, attacker, -selfRecovery);

            foreach (BattleEntity neighbor in GetNeighbors(controller, attacker))
                Add(deltas, neighbor, -neighborRecovery);
        }

        // ③ 治疗暴击：全队减压
        for (int i = 0; i < facts.Count; i++)
        {
            BattleActionFact fact = facts[i];
            if (fact.Kind != BattleFactKind.Heal || !fact.IsCritical)
                continue;

            int recovery = config != null ? Mathf.Max(0, config.healCriticalRecovery) : DefaultHealCriticalRecovery;
            foreach (BattleEntity ally in GetAlivePlayers(controller))
                Add(deltas, ally, -recovery);
        }

        // ④ 排序：被暴击者优先，其余按位置；只产出计划，不写入
        List<BattleEntity> ordered = GetAlivePlayers(controller);
        List<BattleStressChange> planned = new();

        foreach (BattleEntity entity in ordered)
        {
            if (!deltas.TryGetValue(entity, out int delta) || delta == 0)
                continue;

            planned.Add(new BattleStressChange(entity, delta, primary.Contains(entity)));
        }

        planned.Sort((a, b) =>
        {
            if (a.IsPrimary != b.IsPrimary)
                return a.IsPrimary ? -1 : 1;

            return a.Entity.RuntimeData.Position.CompareTo(b.Entity.RuntimeData.Position);
        });

        return planned;
    }

    /// <summary>
    /// 写入一个角色的压力变化。
    /// 返回 false 表示这一条不需要表现（死亡、或确实没有变化）。
    /// ModifyStress 内部会通知 CharacterStressSystem，压力条 UI 在这一刻刷新。
    /// </summary>
    public static bool Apply(BattleStressChange change, out BattleStressOutcome outcome)
    {
        outcome = default;

        if (change.Entity == null || change.Entity.RuntimeData == null || !change.Entity.IsAlive)
            return false;

        CharacterRuntimeData data = change.Entity.RuntimeData;

        // 美德状态不改变数值，但仍然要给出增压 / 减压表现。
        if (data.SpiritState == CharacterSpiritState.Virtue && change.Delta != 0)
        {
            outcome = new BattleStressOutcome(change.Entity, 0, change.Delta);

            Debug.Log($"[压力结算] {change.Entity.Definition?.Name}：美德状态，数值保持 {data.CurrentStress}，" +
                      $"表现仍显示 {change.Delta:+#;-#;0}。");

            return true;
        }

        int before = data.CurrentStress;
        data.ModifyStress(change.Delta);
        int after = data.CurrentStress;

        if (after == before)
        {
            // 已经处在 0 或 200 的边界：数值不变，但仍然给出表现，与美德规则保持一致。
            outcome = new BattleStressOutcome(change.Entity, 0, change.Delta);

            Debug.Log($"[压力结算] {change.Entity.Definition?.Name}：压力已到边界（{before}），" +
                      $"数值不变，表现仍显示 {change.Delta:+#;-#;0}。");

            return true;
        }

        outcome = new BattleStressOutcome(change.Entity, after - before, after - before);

        Debug.Log($"[压力结算] {change.Entity.Definition?.Name}（位置 {data.Position}）" +
                  $"变化 {after - before:+#;-#;0}，{before} → {after}（状态：{data.SpiritState}）。");

        return true;
    }

    /// <summary>队友说话带来的全队压力变化，按位置升序。</summary>
    public static List<BattleStressChange> FromBark(BattleController controller, int delta)
    {
        List<BattleStressChange> changes = new();

        if (controller == null || delta == 0)
            return changes;

        foreach (BattleEntity entity in GetAlivePlayers(controller))
            changes.Add(new BattleStressChange(entity, delta));

        return changes;
    }

    private static void Add(Dictionary<BattleEntity, int> deltas, BattleEntity entity, int amount)
    {
        if (entity == null || amount == 0)
            return;

        deltas.TryGetValue(entity, out int current);
        deltas[entity] = current + amount;
    }

    private static int Roll(BattleStressConfig config, bool victim)
    {
        if (config == null)
        {
            return victim
                ? Random.Range(DefaultVictimMin, DefaultVictimMax + 1)
                : Random.Range(DefaultNeighborMin, DefaultNeighborMax + 1);
        }

        return victim ? config.RollVictimStress() : config.RollNeighborStress();
    }

    /// <summary>按位置取左右各一名存活队友，最多两个。</summary>
    private static List<BattleEntity> GetNeighbors(BattleController controller, BattleEntity victim)
    {
        List<BattleEntity> neighbors = new();
        if (victim.RuntimeData == null)
            return neighbors;

        int position = victim.RuntimeData.Position;

        foreach (BattleEntity ally in GetAlivePlayers(controller))
        {
            if (ally == victim || ally.RuntimeData == null)
                continue;

            int delta = Mathf.Abs(ally.RuntimeData.Position - position);
            if (delta == 1)
                neighbors.Add(ally);
        }

        return neighbors;
    }

    private static List<BattleEntity> GetAlivePlayers(BattleController controller)
    {
        List<BattleEntity> players = new();

        IReadOnlyList<BattleEntity> all = controller.AllEntities;
        if (all == null)
            return players;

        for (int i = 0; i < all.Count; i++)
        {
            BattleEntity entity = all[i];
            if (entity == null || !entity.IsPlayer || !entity.IsAlive || entity.RuntimeData == null)
                continue;

            players.Add(entity);
        }

        players.Sort((a, b) => a.RuntimeData.Position.CompareTo(b.RuntimeData.Position));
        return players;
    }
}

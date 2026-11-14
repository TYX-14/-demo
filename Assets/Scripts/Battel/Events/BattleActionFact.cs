using System.Collections.Generic;
using MFrameork.Event;

/// <summary>一次行动中，某个单位身上发生的具体事实类型。</summary>
public enum BattleFactKind
{
    Damage,
    Heal,
    StressDamage,
    StressHeal,
    Stun,
    Knockback,

    /// <summary>预留：当前结算没有命中判定，不会产生此类型。</summary>
    Miss
}

/// <summary>
/// 一次行动中，某个单位身上发生的一件事。
/// 只描述事实，不携带表现信息（颜色、音效、飘字等由表现层自己决定）。
/// </summary>
public readonly struct BattleActionFact
{
    /// <summary>发起这次行动的单位。</summary>
    public BattleEntity Actor { get; }

    /// <summary>承受这一条事实的单位。</summary>
    public BattleEntity Target { get; }

    public BattleFactKind Kind { get; }

    /// <summary>数值。伤害、治疗、压力为绝对值；眩晕为行动数；击退为格数。</summary>
    public int Amount { get; }

    /// <summary>是否暴击。当前暴击系统尚未接入，恒为 false。</summary>
    public bool IsCritical { get; }

    /// <summary>这一条是否直接导致目标死亡。</summary>
    public bool IsKillingBlow { get; }

    /// <summary>本条事实结算前的生命值。</summary>
    public int HpBefore { get; }

    /// <summary>本条事实结算后的生命值。</summary>
    public int HpAfter { get; }

    /// <summary>承受方是否属于玩家阵营。派生字段，省得每个消费者重复判断。</summary>
    public bool TargetIsPlayer { get; }

    public BattleActionFact(
        BattleEntity actor,
        BattleEntity target,
        BattleFactKind kind,
        int amount,
        bool isCritical,
        int hpBefore,
        int hpAfter,
        bool isKillingBlow = false)
    {
        Actor = actor;
        Target = target;
        Kind = kind;
        Amount = amount;
        IsCritical = isCritical;
        HpBefore = hpBefore;
        HpAfter = hpAfter;
        IsKillingBlow = isKillingBlow;
        TargetIsPlayer = target != null && target.IsPlayer;
    }
}

/// <summary>
/// 一次行动的全部事实汇总。每次行动只发布一次，
/// 发布时机是命中结算完成、特写仍在播放的那一帧。
/// </summary>
public readonly struct BattleActionResolvedEvent : IEvent
{
    public BattleEntity Actor { get; }
    public SkillDefinitionSO Skill { get; }
    public IReadOnlyList<BattleActionFact> Facts { get; }

    public BattleActionResolvedEvent(
        BattleEntity actor,
        SkillDefinitionSO skill,
        IReadOnlyList<BattleActionFact> facts)
    {
        Actor = actor;
        Skill = skill;
        Facts = facts ?? System.Array.Empty<BattleActionFact>();
    }
}

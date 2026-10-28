using System;
using UnityEngine;

/// <summary>
/// 技能即时效果的公共父类。每个子类只保存自己需要的数据。
/// 具体结算由战斗效果执行器负责，说明文本由各子类根据数据生成。
/// </summary>
[Serializable]
public abstract class SkillEffectBase
{
    public abstract string BuildDescription();

    protected static string FormatPercent(float percent)
    {
        return (percent > 0 ? "+" : string.Empty) + percent.ToString("0.##") + "%";
    }

    protected static string BuildConditionSuffix(SkillEffectCondition condition)
    {
        return condition == SkillEffectCondition.TargetSizeGreaterThanOne
            ? "（对体型>1）"
            : string.Empty;
    }
}

/// <summary>表示执行一次伤害结算，不保存固定伤害值；该标记不单独生成技能说明文本。</summary>
[Serializable]
public sealed class DamageEffect : SkillEffectBase
{
    public override string BuildDescription() => string.Empty;
}

/// <summary>治疗效果的配置值。实际恢复量与暴击结算由执行器处理。</summary>
[Serializable]
public sealed class HealEffect : SkillEffectBase
{
    [SerializeField] private float value;

    public float Value => value;

    public HealEffect() { }
    public HealEffect(float value) => this.value = value;

    public override string BuildDescription() => "恢复" + Mathf.Abs(Mathf.RoundToInt(value));
}

/// <summary>压力伤害效果。</summary>
[Serializable]
public sealed class StressDamageEffect : SkillEffectBase
{
    [SerializeField] private float value;

    public float Value => value;

    public StressDamageEffect() { }
    public StressDamageEffect(float value) => this.value = value;

    public override string BuildDescription() => "压力伤害" + Mathf.Abs(Mathf.RoundToInt(value));
}

/// <summary>压力恢复效果。</summary>
[Serializable]
public sealed class StressHealEffect : SkillEffectBase
{
    [SerializeField] private float value;

    public float Value => value;

    public StressHealEffect() { }
    public StressHealEffect(float value) => this.value = value;

    public override string BuildDescription() => "压力恢复" + Mathf.Abs(Mathf.RoundToInt(value));
}

/// <summary>使目标向前移动指定格数。</summary>
[Serializable]
public sealed class MoveForwardEffect : SkillEffectBase
{
    [SerializeField] private float distance = 1f;

    public float Distance => distance;

    public MoveForwardEffect() { }
    public MoveForwardEffect(float distance) => this.distance = distance;

    public override string BuildDescription() => "前进" + Mathf.Abs(Mathf.RoundToInt(distance));
}

/// <summary>使目标向后移动指定格数。</summary>
[Serializable]
public sealed class MoveBackwardEffect : SkillEffectBase
{
    [SerializeField] private float distance = 1f;

    public float Distance => distance;

    public MoveBackwardEffect() { }
    public MoveBackwardEffect(float distance) => this.distance = distance;

    public override string BuildDescription() => "后退" + Mathf.Abs(Mathf.RoundToInt(distance));
}

/// <summary>将行动者与目标交换站位。仅用于同阵营目标。</summary>
[Serializable]
public sealed class SwapPositionEffect : SkillEffectBase
{
    public override string BuildDescription() => "换位";
}

/// <summary>将目标向后推指定槽位数。</summary>
[Serializable]
public sealed class KnockbackEffect : SkillEffectBase
{
    [SerializeField, Min(1)] private int distance = 1;
    public int Distance => Mathf.Max(1, distance);
    public KnockbackEffect() { }
    public KnockbackEffect(int distance) => this.distance = Mathf.Max(1, distance);
    public override string BuildDescription() => "击退" + Distance;
}

/// <summary>令目标跳过指定次数的行动，不属于持续 Buff。</summary>
[Serializable]
public sealed class StunEffect : SkillEffectBase
{
    [SerializeField, Min(1)] private int actions = 1;
    public int Actions => Mathf.Max(1, actions);
    public StunEffect() { }
    public StunEffect(int actions) => this.actions = Mathf.Max(1, actions);
    public override string BuildDescription() => "眩晕" + Actions + "次行动";
}

/// <summary>只影响当前这次攻击的伤害修正，不是角色持续 Buff。</summary>
[Serializable]
public sealed class DamageModifierEffect : SkillEffectBase
{
    [SerializeField] private float percent;
    [SerializeField] private SkillEffectCondition condition;

    public float Percent => percent;
    public SkillEffectCondition Condition => condition;

    public DamageModifierEffect() { }
    public DamageModifierEffect(float percent, SkillEffectCondition condition = SkillEffectCondition.None)
    {
        this.percent = percent;
        this.condition = condition;
    }

    public override string BuildDescription() =>
        "伤害修正" + FormatPercent(percent) + BuildConditionSuffix(condition);
}

/// <summary>只影响当前这次行动的精准修正，不是角色持续 Buff。</summary>
[Serializable]
public sealed class AccuracyModifierEffect : SkillEffectBase
{
    [SerializeField] private float percent;
    [SerializeField] private SkillEffectCondition condition;

    public float Percent => percent;
    public SkillEffectCondition Condition => condition;

    public AccuracyModifierEffect() { }
    public AccuracyModifierEffect(float percent, SkillEffectCondition condition = SkillEffectCondition.None)
    {
        this.percent = percent;
        this.condition = condition;
    }

    public override string BuildDescription() =>
        "精准修正" + FormatPercent(percent) + BuildConditionSuffix(condition);
}

/// <summary>只影响当前这次行动的暴击修正，不是角色持续 Buff。</summary>
[Serializable]
public sealed class CriticalModifierEffect : SkillEffectBase
{
    [SerializeField] private float percent;
    [SerializeField] private SkillEffectCondition condition;

    public float Percent => percent;
    public SkillEffectCondition Condition => condition;

    public CriticalModifierEffect() { }
    public CriticalModifierEffect(float percent, SkillEffectCondition condition = SkillEffectCondition.None)
    {
        this.percent = percent;
        this.condition = condition;
    }

    public override string BuildDescription() =>
        "暴击修正" + FormatPercent(percent) + BuildConditionSuffix(condition);
}

/// <summary>
/// 施加可复用状态定义。持续回合与次数属于本次施加的参数，运行时剩余值由状态实例管理。
/// </summary>
[Serializable]
public sealed class ApplyStatusEffect : SkillEffectBase
{
    [SerializeField] private StatusEffectDefinitionSO statusEffect;
    [Min(0)] [SerializeField] private int durationRounds;
    [Min(0)] [SerializeField] private int charges;

    public StatusEffectDefinitionSO StatusEffect => statusEffect;
    public int DurationRounds => durationRounds;
    public int Charges => charges;

    public ApplyStatusEffect() { }
    public ApplyStatusEffect(StatusEffectDefinitionSO statusEffect, int durationRounds, int charges)
    {
        this.statusEffect = statusEffect;
        this.durationRounds = durationRounds;
        this.charges = charges;
    }

    public override string BuildDescription()
    {
        if (statusEffect == null)
            return string.Empty;

        string duration = durationRounds > 0 ? "（" + durationRounds + "回合）" : string.Empty;
        string chargeText = charges > 0 ? "（" + charges + "次）" : string.Empty;
        return statusEffect.StatusName + duration + chargeText;
    }
}

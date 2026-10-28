using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 技能即时效果类型。数值修正属于本次行动，不是角色 Buff。
/// </summary>
public enum SkillEffectType
{
    Heal,
    StressDamage,
    StressHeal,
    MoveForward,
    MoveBackward,
    DamageModifier,
    AccuracyModifier,
    CriticalModifier,
    ApplyStatus
}

/// <summary>即时效果的可选目标条件。</summary>
public enum SkillEffectCondition
{
    None,
    TargetSizeGreaterThanOne
}

/// <summary>
/// 技能效果的数据配置。它只描述效果，不负责执行结算。
/// 普通效果数据内嵌在技能等级配置中，避免每个数值都创建独立资产。
/// </summary>
[Serializable]
public class SkillEffectData
{
    [SerializeField] private SkillEffectType effectType;
    [SerializeField] private float value;
    [SerializeField] private SkillEffectCondition condition;
    [SerializeField] private StatusEffectDefinitionSO statusEffect;
    [Min(0)] [SerializeField] private int durationRounds;
    [Min(0)] [SerializeField] private int charges;

    public SkillEffectType EffectType => effectType;
    public float Value => value;
    public SkillEffectCondition Condition => condition;
    public StatusEffectDefinitionSO StatusEffect => statusEffect;
    public int DurationRounds => durationRounds;
    public int Charges => charges;

    /// <summary>
    /// 为技能说明生成简洁效果行。实际结算仍由后续的效果执行系统完成。
    /// Value 对三个修正项使用百分数表示，例如 -50 表示 -50%。
    /// </summary>
    public string BuildDescription()
    {
        switch (effectType)
        {
            case SkillEffectType.Heal:
                return "恢复" + Mathf.Abs(Mathf.RoundToInt(value));
            case SkillEffectType.StressDamage:
                return "压力伤害" + Mathf.Abs(Mathf.RoundToInt(value));
            case SkillEffectType.StressHeal:
                return "压力恢复" + Mathf.Abs(Mathf.RoundToInt(value));
            case SkillEffectType.MoveForward:
                return "前进" + Mathf.Abs(Mathf.RoundToInt(value));
            case SkillEffectType.MoveBackward:
                return "后退" + Mathf.Abs(Mathf.RoundToInt(value));
            case SkillEffectType.DamageModifier:
                return "伤害修正" + FormatPercent(value) + BuildConditionSuffix();
            case SkillEffectType.AccuracyModifier:
                return "精准修正" + FormatPercent(value) + BuildConditionSuffix();
            case SkillEffectType.CriticalModifier:
                return "暴击修正" + FormatPercent(value) + BuildConditionSuffix();
            case SkillEffectType.ApplyStatus:
                if (statusEffect == null)
                    return string.Empty;

                string duration = durationRounds > 0 ? "（" + durationRounds + "回合）" : string.Empty;
                string chargeText = charges > 0 ? "（" + charges + "次）" : string.Empty;
                return statusEffect.StatusName + duration + chargeText;
            default:
                return string.Empty;
        }
    }

    private string BuildConditionSuffix()
    {
        return condition == SkillEffectCondition.TargetSizeGreaterThanOne ? "（对体型>1）" : string.Empty;
    }

    private static string FormatPercent(float percent)
    {
        return (percent > 0 ? "+" : string.Empty) + percent.ToString("0.##") + "%";
    }
}

/// <summary>某一技能等级独立配置的效果列表。</summary>
[Serializable]
public class SkillLevelConfig
{
    [Min(1)] [SerializeField] private int level = 1;
    [SerializeField] private List<SkillEffectData> effects = new();

    public int Level => level;
    public IReadOnlyList<SkillEffectData> Effects => effects;
}

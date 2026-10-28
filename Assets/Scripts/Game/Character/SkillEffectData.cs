using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 旧版效果类型枚举。保留用于读取并迁移旧技能资源。
/// 新技能效果使用 SkillEffectBase 的具体子类，不再依赖这个枚举执行。
/// </summary>
public enum SkillEffectType
{
    Damage,
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
/// 旧版通用效果数据。不要删除或改名 effects 字段，Unity 旧资产仍依赖它反序列化。
/// 新数据迁移完成并确认所有资产无旧数据后，才适合在后续阶段移除。
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

    public string BuildDescription()
    {
        switch (effectType)
        {
            case SkillEffectType.Damage:
                return string.Empty;
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

/// <summary>
/// 单个技能等级的效果配置。
/// effects 字段保留旧格式以兼容已有 Unity 资产；新效果写入 typedEffects。
/// </summary>
[Serializable]
public class SkillLevelConfig
{
    [Min(1)] [SerializeField] private int level = 1;

    // 保留原字段名和原字段类型，确保已有 YAML 资产仍能被 Unity 读取。
    [SerializeField, HideInInspector] private List<SkillEffectData> effects = new();

    [SerializeReference] private List<SkillEffectBase> typedEffects = new();

    [NonSerialized] private List<SkillEffectBase> legacyConvertedCache;

    public int Level => level;

    /// <summary>读取新效果；若该等级仍是旧格式，则在内存中转换为新类型供说明和执行器使用。</summary>
    public IReadOnlyList<SkillEffectBase> Effects
    {
        get
        {
            if (typedEffects != null && typedEffects.Count > 0)
                return typedEffects;

            if (effects == null || effects.Count == 0)
                return typedEffects;

            if (legacyConvertedCache == null)
            {
                legacyConvertedCache = new List<SkillEffectBase>();
                foreach (SkillEffectData oldEffect in effects)
                {
                    SkillEffectBase converted = ConvertLegacyEffect(oldEffect);
                    if (converted != null)
                        legacyConvertedCache.Add(converted);
                }
            }

            return legacyConvertedCache;
        }
    }

    public bool HasLegacyEffects => effects != null && effects.Count > 0;
    public bool HasTypedEffects => typedEffects != null && typedEffects.Count > 0;

    /// <summary>
    /// 将旧格式转换到新列表。旧字段暂时保留作为备份，不会在迁移时删除。
    /// 返回 true 表示本次写入了新效果。
    /// </summary>
    public bool MigrateLegacyEffects()
    {
        if (HasTypedEffects || !HasLegacyEffects)
            return false;

        typedEffects = new List<SkillEffectBase>();
        foreach (SkillEffectData oldEffect in effects)
        {
            SkillEffectBase converted = ConvertLegacyEffect(oldEffect);
            if (converted != null)
                typedEffects.Add(converted);
        }

        legacyConvertedCache = null;
        return typedEffects.Count > 0;
    }

    private static SkillEffectBase ConvertLegacyEffect(SkillEffectData oldEffect)
    {
        if (oldEffect == null)
            return null;

        switch (oldEffect.EffectType)
        {
            case SkillEffectType.Damage:
                return new DamageEffect();
            case SkillEffectType.Heal:
                return new HealEffect(oldEffect.Value);
            case SkillEffectType.StressDamage:
                return new StressDamageEffect(oldEffect.Value);
            case SkillEffectType.StressHeal:
                return new StressHealEffect(oldEffect.Value);
            case SkillEffectType.MoveForward:
                return new MoveForwardEffect(oldEffect.Value);
            case SkillEffectType.MoveBackward:
                return new MoveBackwardEffect(oldEffect.Value);
            case SkillEffectType.DamageModifier:
                return new DamageModifierEffect(oldEffect.Value, oldEffect.Condition);
            case SkillEffectType.AccuracyModifier:
                return new AccuracyModifierEffect(oldEffect.Value, oldEffect.Condition);
            case SkillEffectType.CriticalModifier:
                return new CriticalModifierEffect(oldEffect.Value, oldEffect.Condition);
            case SkillEffectType.ApplyStatus:
                return new ApplyStatusEffect(oldEffect.StatusEffect, oldEffect.DurationRounds, oldEffect.Charges);
            default:
                return null;
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SkillTargetType
{
    SingleTarget,
    AoE
}

/// <summary>目标阵营/关系。AllEntities 表示双方所有存活实体，不受目标站位列表限制。</summary>
public enum SkillTargetRelation
{
    Enemy,
    Ally,
    Self,
    AllEntities
}

/// <summary>目标取得方式，与单体/AoE 范围相互独立。</summary>
public enum SkillTargetSelectionMode
{
    Manual,
    Automatic,
    Self,
    None
}

/// <summary>
/// 技能静态定义。
/// 只保存技能配置，不直接执行伤害、状态或角色移动逻辑。
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill Definition")]
public class SkillDefinitionSO : ScriptableObject
{
    [Header("基本信息")]
    [SerializeField] private string skillName;
    [SerializeField] private Sprite skillIcon;
    [Tooltip("尚未配置等级效果时使用的备用说明。配置效果后，技能说明会根据等级效果自动生成。")]
    [TextArea(2, 5)]
    [SerializeField] private string description;

    [Header("资源消耗")]
    [Min(0)]
    [SerializeField] private int spCost;

    [Header("目标规则")]
    [Tooltip("目标关系与单体/AoE 独立配置。Enemy=敌方，Ally=友方（不含自身），Self=自身，AllEntities=双方所有存活实体。")]
    [SerializeField] private SkillTargetRelation targetRelation = SkillTargetRelation.Enemy;

    [Tooltip("单体只影响一个目标；AoE 影响符合目标关系与站位规则的所有目标。")]
    [SerializeField] private SkillTargetType targetType = SkillTargetType.SingleTarget;

    [Tooltip("Manual=进入玩家目标选择；Automatic=自动取得目标；Self=自动以自身为目标；None=不需要实体目标。")]
    [SerializeField] private SkillTargetSelectionMode targetSelectionMode = SkillTargetSelectionMode.Manual;

    [Header("站位规则")]
    [Tooltip("纯敌方目标技能的使用者必须处于这些站位（1~4）。允许选择友方、自身或全体实体的技能不检查使用者站位。")]
    [SerializeField] private List<int> usablePositions = new() { 1, 2, 3, 4 };

    [Tooltip("目标可选/覆盖的站位（1~4）。Self 与 AllEntities 不受此列表限制；其他关系按目标自身站位筛选。")]
    [SerializeField] private List<int> targetPositions = new() { 1, 2, 3, 4 };

    [Header("技能等级与效果")]
    [Tooltip("每一级分别配置效果数值。升级只改变该角色使用的等级，不修改共享技能资产。")]
    [SerializeField] private List<SkillLevelConfig> levelConfigs = new();

    public string SkillName => skillName;
    public Sprite SkillIcon => skillIcon;
    public string Description => description;
    public int SpCost => spCost;
    public SkillTargetRelation TargetRelation => targetRelation;
    public SkillTargetType TargetType => targetType;
    public SkillTargetSelectionMode TargetSelectionMode => targetSelectionMode;
    public IReadOnlyList<int> UsablePositions => usablePositions;
    public IReadOnlyList<int> TargetPositions => targetPositions;
    public IReadOnlyList<SkillLevelConfig> LevelConfigs => levelConfigs;

    public int MaxLevel
    {
        get
        {
            int maxLevel = 1;
            if (levelConfigs == null)
                return maxLevel;

            foreach (SkillLevelConfig config in levelConfigs)
            {
                if (config != null && config.Level > maxLevel)
                    maxLevel = config.Level;
            }

            return maxLevel;
        }
    }

    /// <summary>读取指定等级配置；若缺少该等级，返回等级编号最接近且不高于目标等级的配置。</summary>
    public SkillLevelConfig GetLevelConfig(int level)
    {
        if (levelConfigs == null || levelConfigs.Count == 0)
            return null;

        SkillLevelConfig best = null;
        SkillLevelConfig lowest = null;

        foreach (SkillLevelConfig config in levelConfigs)
        {
            if (config == null)
                continue;

            if (lowest == null || config.Level < lowest.Level)
                lowest = config;

            if (config.Level <= Mathf.Max(1, level) &&
                (best == null || config.Level > best.Level))
            {
                best = config;
            }
        }

        return best ?? lowest;
    }

    /// <summary>
    /// 返回指定等级的动态说明。效果列表有内容时，以效果数据生成说明；
    /// 未配置效果时保留原有 description，避免旧技能资产显示为空。
    /// </summary>
    public string GetDescriptionForLevel(int level)
    {
        SkillLevelConfig config = GetLevelConfig(level);
        if (config == null || config.Effects == null || config.Effects.Count == 0)
            return description;

        StringBuilder builder = new StringBuilder();
        foreach (SkillEffectData effect in config.Effects)
        {
            if (effect == null)
                continue;

            string line = effect.BuildDescription();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(line);
        }

        return builder.Length > 0 ? builder.ToString() : description;
    }
}

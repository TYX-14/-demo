using System.Collections.Generic;
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

    public string SkillName => skillName;
    public Sprite SkillIcon => skillIcon;
    public string Description => description;
    public int SpCost => spCost;
    public SkillTargetRelation TargetRelation => targetRelation;
    public SkillTargetType TargetType => targetType;
    public SkillTargetSelectionMode TargetSelectionMode => targetSelectionMode;
    public IReadOnlyList<int> UsablePositions => usablePositions;
    public IReadOnlyList<int> TargetPositions => targetPositions;
}

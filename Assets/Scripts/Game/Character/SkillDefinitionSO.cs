using System.Collections.Generic;
using UnityEngine;

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

    [Header("站位规则")]
    [Tooltip("角色自身处于哪些站位时可以使用该技能，站位编号为 1~4。")]
    [SerializeField] private List<int> usablePositions = new() { 1, 2, 3, 4 };

    [Tooltip("该技能可以选择哪些目标站位，站位编号为 1~4。")]
    [SerializeField] private List<int> targetPositions = new() { 1, 2, 3, 4 };

    public string SkillName => skillName;
    public Sprite SkillIcon => skillIcon;
    public string Description => description;
    public int SpCost => spCost;
    public IReadOnlyList<int> UsablePositions => usablePositions;
    public IReadOnlyList<int> TargetPositions => targetPositions;
}

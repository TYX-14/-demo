using System;
using UnityEngine;

/// <summary>
/// 某个角色拥有的技能进度。
/// 技能等级属于角色运行数据，不写回所有角色共用的 SkillDefinitionSO。
/// </summary>
[Serializable]
public class SkillRuntimeData
{
    [SerializeField] private SkillDefinitionSO definition;
    [Min(1)] [SerializeField] private int level = 1;

    public SkillDefinitionSO Definition => definition;
    public int Level => level;

    public SkillRuntimeData(SkillDefinitionSO skillDefinition, int initialLevel = 1)
    {
        definition = skillDefinition;
        SetLevel(initialLevel);
    }

    public void SetLevel(int newLevel)
    {
        int maxLevel = definition != null ? definition.MaxLevel : 1;
        level = Mathf.Clamp(newLevel, 1, Mathf.Max(1, maxLevel));
    }
}

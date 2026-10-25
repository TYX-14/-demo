using System.Collections.Generic;

/// <summary>
/// 当前待执行行动的数据载体。
/// 只保存行动者、技能、玩家选择的输入位置以及解析后的实际目标，不负责监听输入或结算效果。
/// </summary>
public sealed class BattleActionContext
{
    public BattleEntity Actor { get; }
    public SkillDefinitionSO Skill { get; }
    public int SelectedInputPosition { get; private set; }
    public BattleEntity SelectedInputTarget { get; private set; }
    public IReadOnlyList<BattleEntity> ResolvedTargets => _resolvedTargets;

    private readonly List<BattleEntity> _resolvedTargets = new();

    public BattleActionContext(BattleEntity actor, SkillDefinitionSO skill)
    {
        Actor = actor;
        Skill = skill;
    }

    /// <summary>
    /// 记录玩家点击的位置，并根据技能类型解析实际受击目标。
    /// 单体技能只命中被点击的目标；AoE 技能命中 TargetPositions 覆盖范围内的所有存活敌人。
    /// </summary>
    public bool TrySetTarget(BattleEntity selectedTarget, IReadOnlyList<BattleEntity> allEntities,
        System.Func<BattleEntity, int> getPosition)
    {
        if (Skill == null || selectedTarget == null || allEntities == null || getPosition == null)
            return false;

        if (!selectedTarget.IsAlive || selectedTarget.IsPlayer)
            return false;

        int selectedPosition = getPosition(selectedTarget);
        if (!ContainsPosition(Skill.TargetPositions, selectedPosition))
            return false;

        SelectedInputPosition = selectedPosition;
        SelectedInputTarget = selectedTarget;
        _resolvedTargets.Clear();

        if (Skill.TargetType == SkillTargetType.SingleTarget)
        {
            _resolvedTargets.Add(selectedTarget);
            return true;
        }

        // AoE 的点击只是一次输入。实际受击目标由技能配置的覆盖位置统一解析。
        foreach (BattleEntity entity in allEntities)
        {
            if (entity == null || !entity.IsAlive || entity.IsPlayer)
                continue;

            if (ContainsPosition(Skill.TargetPositions, getPosition(entity)))
                _resolvedTargets.Add(entity);
        }

        return _resolvedTargets.Count > 0;
    }

    private static bool ContainsPosition(IReadOnlyList<int> positions, int position)
    {
        if (positions == null)
            return false;

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i] == position)
                return true;
        }

        return false;
    }
}

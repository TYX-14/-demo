using System.Collections.Generic;

/// <summary>
/// 当前待执行行动的数据载体。
/// 只保存行动者、技能、玩家选择的输入位置以及目标规则层解析后的实际目标。
/// 不负责判断目标是否合法，也不负责解析范围。
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

    /// <summary>接收 BattleTargeting 已解析的结果，只负责保存行动数据。</summary>
    public void SetResolvedTargets(BattleEntity selectedInputTarget, int selectedInputPosition,
        IReadOnlyList<BattleEntity> resolvedTargets)
    {
        SelectedInputTarget = selectedInputTarget;
        SelectedInputPosition = selectedInputPosition;
        _resolvedTargets.Clear();

        if (resolvedTargets == null)
            return;

        for (int i = 0; i < resolvedTargets.Count; i++)
        {
            BattleEntity target = resolvedTargets[i];
            if (target != null && !_resolvedTargets.Contains(target))
                _resolvedTargets.Add(target);
        }
    }
}

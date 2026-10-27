using System;
using System.Collections.Generic;

/// <summary>
/// 战斗目标规则的唯一解析入口。
/// 读取 SkillDefinitionSO 中的目标关系、范围、选择方式与站位配置；调用方只负责输入或决策。
/// </summary>
public static class BattleTargeting
{
    /// <summary>
    /// 是否必须进入玩家手动目标选择状态。
    /// 单体/AoE 类型不会改变此判断。
    /// </summary>
    public static bool RequiresManualSelection(SkillDefinitionSO skill)
    {
        if (skill == null)
            return false;

        // 自身目标也必须由玩家确认，不能因为目标只有施法者本人就自动发动。
        // 同时兼容目标选择模式与目标关系两种配置入口。
        return skill.TargetSelectionMode == SkillTargetSelectionMode.Manual ||
               skill.TargetSelectionMode == SkillTargetSelectionMode.Self ||
               skill.TargetRelation == SkillTargetRelation.Self;
    }

    /// <summary>
    /// 检查行动者能否从当前位置使用技能。
    /// 按项目规则，允许选择友方、自身或全体实体的技能不限制行动者站位。
    /// </summary>
    public static bool CanUseFromPosition(SkillDefinitionSO skill, int actorPosition)
    {
        if (skill == null)
            return false;

        if (skill.TargetRelation != SkillTargetRelation.Enemy ||
            skill.TargetSelectionMode == SkillTargetSelectionMode.Self)
            return true;

        return ContainsPosition(skill.UsablePositions, actorPosition);
    }

    /// <summary>判断某个实体是否符合技能配置的目标关系及目标站位。</summary>
    public static bool IsLegalTarget(SkillDefinitionSO skill, BattleEntity actor,
        BattleEntity candidate, Func<BattleEntity, int> getPosition)
    {
        if (skill == null || actor == null || candidate == null ||
            !actor.IsAlive || !candidate.IsAlive)
            return false;

        switch (skill.TargetRelation)
        {
            case SkillTargetRelation.Enemy:
                if (candidate.IsPlayer == actor.IsPlayer)
                    return false;
                break;
            case SkillTargetRelation.Ally:
                // Ally 表示同阵营实体，包含行动者自身；是否允许自身成为目标由 TargetPositions 等技能配置决定。
                if (candidate.IsPlayer != actor.IsPlayer)
                    return false;
                break;
            case SkillTargetRelation.Self:
                return candidate == actor;
            case SkillTargetRelation.AllEntities:
                return true;
            default:
                return false;
        }

        if (getPosition == null)
            return false;

        return ContainsPosition(skill.TargetPositions, getPosition(candidate));
    }

    /// <summary>获取当前所有合法候选目标，供玩家输入、敌方 AI 或后续自动选目标逻辑复用。</summary>
    public static List<BattleEntity> GetLegalTargets(SkillDefinitionSO skill, BattleEntity actor,
        IReadOnlyList<BattleEntity> allEntities, Func<BattleEntity, int> getPosition)
    {
        List<BattleEntity> result = new();
        if (skill == null || actor == null || allEntities == null)
            return result;

        for (int i = 0; i < allEntities.Count; i++)
        {
            BattleEntity candidate = allEntities[i];
            if (IsLegalTarget(skill, actor, candidate, getPosition))
                result.Add(candidate);
        }

        return result;
    }

    /// <summary>
    /// 解析玩家手动点击的目标。单体只解析被点击者；AoE 将关系与站位规则覆盖到的所有目标纳入结果。
    /// </summary>
    public static bool TryResolveSelectedTarget(SkillDefinitionSO skill, BattleEntity actor,
        BattleEntity selectedTarget, IReadOnlyList<BattleEntity> allEntities,
        Func<BattleEntity, int> getPosition, out int selectedPosition,
        out List<BattleEntity> resolvedTargets)
    {
        selectedPosition = 0;
        resolvedTargets = new List<BattleEntity>();

        if (skill == null || selectedTarget == null ||
            !IsLegalTarget(skill, actor, selectedTarget, getPosition))
            return false;

        selectedPosition = getPosition != null ? getPosition(selectedTarget) : 0;

        if (skill.TargetType == SkillTargetType.SingleTarget)
        {
            resolvedTargets.Add(selectedTarget);
            return true;
        }

        resolvedTargets = GetLegalTargets(skill, actor, allEntities, getPosition);
        return resolvedTargets.Count > 0;
    }

    /// <summary>
    /// 解析不需要玩家点击目标的技能。
    /// Automatic + SingleTarget 当前采用候选列表中的第一个合法目标；AI 以后可先自行挑选候选目标，
    /// 再通过手动目标解析入口提交，不需要复制合法性规则。
    /// </summary>
    public static bool TryResolveAutomaticTargets(SkillDefinitionSO skill, BattleEntity actor,
        IReadOnlyList<BattleEntity> allEntities, Func<BattleEntity, int> getPosition,
        out BattleEntity selectedInputTarget, out int selectedPosition,
        out List<BattleEntity> resolvedTargets)
    {
        selectedInputTarget = null;
        selectedPosition = 0;
        resolvedTargets = new List<BattleEntity>();

        if (skill == null || actor == null ||
            skill.TargetSelectionMode == SkillTargetSelectionMode.Manual)
            return false;

        if (skill.TargetSelectionMode == SkillTargetSelectionMode.None)
            return true;

        if (skill.TargetSelectionMode == SkillTargetSelectionMode.Self ||
            skill.TargetRelation == SkillTargetRelation.Self)
        {
            if (!actor.IsAlive)
                return false;

            selectedInputTarget = actor;
            selectedPosition = getPosition != null ? getPosition(actor) : 0;
            resolvedTargets.Add(actor);
            return true;
        }

        List<BattleEntity> candidates = GetLegalTargets(skill, actor, allEntities, getPosition);
        if (candidates.Count == 0)
            return false;

        if (skill.TargetType == SkillTargetType.AoE)
        {
            resolvedTargets = candidates;
            return true;
        }

        selectedInputTarget = candidates[0];
        selectedPosition = getPosition != null ? getPosition(selectedInputTarget) : 0;
        resolvedTargets.Add(selectedInputTarget);
        return true;
    }

    private static bool ContainsPosition(IReadOnlyList<int> positions, int position)
    {
        if (positions == null || position < 1 || position > 4)
            return false;

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i] == position)
                return true;
        }

        return false;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人生成与布阵服务。只创建敌人运行时数据并分配逻辑站位，不实例化场景物体。
/// </summary>
public static class EnemySpawnManager
{
    private sealed class PlacementCandidate
    {
        public int OriginalIndex;
        public int OccupiedCount;
        public int PreferredMin;
        public int PreferredMax;
    }

    public static bool TryCreateAutomaticFormation(IList<CharacterDefinitionSO> definitions, out List<CharacterRuntimeData> enemies, out string error)
    {
        enemies = new List<CharacterRuntimeData>();
        error = null;
        if (definitions == null)
        {
            error = "敌人定义列表为空。";
            return false;
        }

        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                enemies.Add(new CharacterRuntimeData(definitions[i]));

        if (enemies.Count == 0)
        {
            error = "本场遭遇没有可生成的敌人。";
            return false;
        }

        if (!TryAssignAutomaticPositions(enemies, out error))
        {
            enemies.Clear();
            return false;
        }
        return true;
    }

    public static bool TryCreateFromEncounter(BattleEncounterSO encounter, out List<CharacterRuntimeData> enemies, out string error)
    {
        enemies = new List<CharacterRuntimeData>();
        error = null;
        if (encounter == null)
        {
            error = "遭遇配置为空。";
            return false;
        }

        if (encounter.placementMode == EnemyEncounterPlacementMode.Automatic)
        {
            if (encounter.enemyEntries == null)
            {
                error = "自动布阵模式没有敌人列表。";
                return false;
            }

            for (int i = 0; i < encounter.enemyEntries.Count; i++)
            {
                EnemyEncounterEntry entry = encounter.enemyEntries[i];
                if (entry == null || entry.definition == null)
                {
                    error = $"自动布阵敌人列表第 {i + 1} 项缺少敌人定义。";
                    return false;
                }
                if (entry.count < 1)
                {
                    error = $"敌人「{entry.definition.Name}」的数量必须至少为 1。";
                    return false;
                }
                for (int count = 0; count < entry.count; count++)
                    enemies.Add(new CharacterRuntimeData(entry.definition));
            }

            if (enemies.Count == 0)
            {
                error = "自动布阵遭遇没有配置任何敌人。";
                return false;
            }
            if (!TryAssignAutomaticPositions(enemies, out error))
            {
                enemies.Clear();
                return false;
            }
            return true;
        }

        if (encounter.fixedPlacements == null || encounter.fixedPlacements.Count == 0)
        {
            error = "固定站位模式没有配置固定敌人列表。";
            return false;
        }

        for (int i = 0; i < encounter.fixedPlacements.Count; i++)
        {
            EnemyFixedPlacement placement = encounter.fixedPlacements[i];
            if (placement == null || placement.definition == null)
            {
                error = $"固定站位列表第 {i + 1} 项缺少敌人定义。";
                return false;
            }
            CharacterRuntimeData runtimeData = new CharacterRuntimeData(placement.definition);
            runtimeData.Position = placement.startingPosition;
            enemies.Add(runtimeData);
        }

        if (!ValidateAssignedFormation(enemies, out error))
        {
            enemies.Clear();
            return false;
        }
        return true;
    }

    private static bool TryAssignAutomaticPositions(List<CharacterRuntimeData> enemies, out string error)
    {
        error = null;
        if (enemies == null || enemies.Count == 0)
        {
            error = "没有可布阵的敌人。";
            return false;
        }

        var candidates = new List<PlacementCandidate>(enemies.Count);
        int totalOccupiedSlots = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterRuntimeData member = enemies[i];
            if (member == null || member.Definition == null)
            {
                error = $"敌人列表第 {i + 1} 项缺少运行时数据或角色定义。";
                return false;
            }
            GetEnemySettings(member.Definition, out int count, out int preferredMin, out int preferredMax);
            totalOccupiedSlots += count;
            candidates.Add(new PlacementCandidate
            {
                OriginalIndex = i,
                OccupiedCount = count,
                PreferredMin = preferredMin,
                PreferredMax = preferredMax
            });
            member.Position = 0;
        }

        if (totalOccupiedSlots > 4)
        {
            error = $"敌人总占格数为 {totalOccupiedSlots}，超过战场的 4 个逻辑位置，无法生成无冲突阵型。";
            return false;
        }

        candidates.Sort((a, b) =>
        {
            int sizeOrder = b.OccupiedCount.CompareTo(a.OccupiedCount);
            if (sizeOrder != 0) return sizeOrder;
            int preferenceOrder = (a.PreferredMax - a.PreferredMin).CompareTo(b.PreferredMax - b.PreferredMin);
            return preferenceOrder != 0 ? preferenceOrder : a.OriginalIndex.CompareTo(b.OriginalIndex);
        });

        bool[] occupied = new bool[5];
        int[] current = new int[enemies.Count];
        int[] best = new int[enemies.Count];
        int bestScore = int.MaxValue;
        SearchFormation(0, 0, candidates, occupied, current, best, ref bestScore);

        if (bestScore == int.MaxValue)
        {
            error = "找不到满足连续占格与不重叠规则的敌方阵型。";
            return false;
        }
        for (int i = 0; i < enemies.Count; i++)
            enemies[i].Position = best[i];
        return true;
    }

    private static void SearchFormation(int index, int score, List<PlacementCandidate> candidates, bool[] occupied, int[] current, int[] best, ref int bestScore)
    {
        if (score >= bestScore) return;
        if (index >= candidates.Count)
        {
            bestScore = score;
            Array.Copy(current, best, current.Length);
            return;
        }

        PlacementCandidate candidate = candidates[index];
        int maxStart = 5 - candidate.OccupiedCount;
        for (int start = 1; start <= maxStart; start++)
        {
            bool canPlace = true;
            int penalty = 0;
            for (int offset = 0; offset < candidate.OccupiedCount; offset++)
            {
                int slot = start + offset;
                if (occupied[slot])
                {
                    canPlace = false;
                    break;
                }
                if (slot < candidate.PreferredMin) penalty += candidate.PreferredMin - slot;
                else if (slot > candidate.PreferredMax) penalty += slot - candidate.PreferredMax;
            }
            if (!canPlace || score + penalty >= bestScore) continue;

            for (int offset = 0; offset < candidate.OccupiedCount; offset++)
                occupied[start + offset] = true;
            current[candidate.OriginalIndex] = start;
            SearchFormation(index + 1, score + penalty, candidates, occupied, current, best, ref bestScore);
            for (int offset = 0; offset < candidate.OccupiedCount; offset++)
                occupied[start + offset] = false;
        }
    }

    public static bool ValidateAssignedFormation(IList<CharacterRuntimeData> enemies, out string error)
    {
        error = null;
        if (enemies == null || enemies.Count == 0)
        {
            error = "敌人列表为空。";
            return false;
        }

        bool[] occupied = new bool[5];
        string[] occupantNames = new string[5];
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterRuntimeData member = enemies[i];
            if (member == null || member.Definition == null)
            {
                error = $"第 {i + 1} 只敌人缺少运行时数据或角色定义。";
                return false;
            }
            GetEnemySettings(member.Definition, out int count, out int preferredMin, out int preferredMax);
            int start = member.Position;
            if (start < 1 || start > 4 || start + count - 1 > 4)
            {
                error = $"敌人「{member.Definition.Name}」的起始位置 {start} 与占格数量 {count} 超出 1～4 号位范围。";
                return false;
            }
            for (int offset = 0; offset < count; offset++)
            {
                int slot = start + offset;
                if (occupied[slot])
                {
                    error = $"敌人「{member.Definition.Name}」与「{occupantNames[slot]}」都占用了逻辑位置 {slot}，阵型存在冲突。";
                    return false;
                }
                occupied[slot] = true;
                occupantNames[slot] = member.Definition.Name;
            }
        }
        return true;
    }

    private static void GetEnemySettings(CharacterDefinitionSO definition, out int occupiedCount, out int preferredMin, out int preferredMax)
    {
        EnemyDfinitionSo enemy = definition as EnemyDfinitionSo;
        if (enemy == null)
        {
            occupiedCount = 1;
            preferredMin = 1;
            preferredMax = 4;
            return;
        }
        occupiedCount = Mathf.Clamp(enemy.occupiedSlotCount, 1, 4);
        preferredMin = Mathf.Clamp(enemy.preferredPositionMin, 1, 4);
        preferredMax = Mathf.Clamp(enemy.preferredPositionMax, preferredMin, 4);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyEncounterPlacementMode
{
    Automatic,
    Fixed
}

[Serializable]
public class EnemyEncounterEntry
{
    public EnemyDfinitionSo definition;
    [Min(1)] public int count = 1;
}

[Serializable]
public class EnemyFixedPlacement
{
    public EnemyDfinitionSo definition;

    [Range(1, 4)]
    [Tooltip("该只敌人的起始逻辑位置。1、2 为前排，3、4 为后排。大型敌人会从该位置开始连续占格。")]
    public int startingPosition = 1;
}

/// <summary>
/// 单场战斗的敌人组合配置。Automatic 模式自动布阵；Fixed 模式每条目配置一只敌人及其起始位置。
/// </summary>
[CreateAssetMenu(menuName = "Battle/Encounter", fileName = "BattleEncounter")]
public class BattleEncounterSO : ScriptableObject
{
    public EnemyEncounterPlacementMode placementMode = EnemyEncounterPlacementMode.Automatic;

    [Header("自动布阵敌人")]
    public List<EnemyEncounterEntry> enemyEntries = new List<EnemyEncounterEntry>();

    [Header("固定站位敌人（每条目代表一只）")]
    public List<EnemyFixedPlacement> fixedPlacements = new List<EnemyFixedPlacement>();
}

using UnityEngine;

/// <summary>
/// 战斗压力的全局数值配置。
/// 放在 Assets 里由 BattleController 引用；未指定时使用代码里的默认值。
/// </summary>
[CreateAssetMenu(fileName = "BattleStressConfig", menuName = "Battle/Stress Config")]
public class BattleStressConfig : ScriptableObject
{
    [Header("受暴击波及")]
    [Tooltip("被暴击者获得的最小压力。")]
    public int victimMin = 6;

    [Tooltip("被暴击者获得的最大压力（含）。")]
    public int victimMax = 8;

    [Tooltip("左右邻居获得的最小压力。")]
    public int neighborMin = 3;

    [Tooltip("左右邻居获得的最大压力（含）。")]
    public int neighborMax = 4;

    [Header("治疗暴击")]
    [Tooltip("治疗暴击时全队减压的点数。")]
    public int healCriticalRecovery = 4;

    [Header("我方暴击")]
    [Tooltip("我方角色打出暴击时，该角色恢复的压力点数。")]
    public int critRecoverySelf = 3;

    [Tooltip("我方角色打出暴击时，左右邻居恢复的压力点数。")]
    public int critRecoveryNeighbor = 2;

    public int RollVictimStress() => Random.Range(Mathf.Min(victimMin, victimMax), Mathf.Max(victimMin, victimMax) + 1);

    public int RollNeighborStress() => Random.Range(Mathf.Min(neighborMin, neighborMax), Mathf.Max(neighborMin, neighborMax) + 1);
}

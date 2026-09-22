using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战场管理器（更新版）。
/// 负责管理战场的生成、布局、单位集合以及敌对单位的阵型。
/// 实现了战斗单位的动态创建、销毁及初始位置映射。
/// </summary>
public class BattleFieldManager : MonoBehaviour
{
    [Header("Prefab")]
    /// <summary>
    /// 战斗单位的预制体引用。
    /// 用于在实例化生成单位时作为对象蓝图。
    /// </summary>
    [SerializeField] private BattleUnit battleUnitPrefab;

    /// <summary>
    /// 战场布局计算器的引用。
    /// 负责提供站位坐标的计算。
    /// </summary>
    private BattleFieldLayout layout;

    /// <summary>
    /// 所有友方单位的根节点（用于保持 Hierarchy 整洁及相对坐标管理）。
    /// </summary>
    private Transform allyRoot;

    /// <summary>
    /// 所有敌方单位的根节点（用于保持 Hierarchy 整洁及相对坐标管理）。
    /// </summary>
    private Transform enemyRoot;

    /// <summary>
    /// 记录每个战斗单位（BattleUnit）对应的最终战斗阵型站位（Home Position）。
    /// 用于在后续状态（如 BattleSetupState）控制单位执行“跑进场”动画。
    /// </summary>
    private readonly Dictionary<BattleUnit, Vector3> _homePos = new();

    /// <summary>
    /// 当前已生成的友方单位列表。
    /// </summary>
    private readonly List<BattleUnit> _spawnedAllyUnits = new();

    /// <summary>
    /// 当前已生成的敌方单位列表。
    /// </summary>
    private readonly List<BattleUnit> _spawnedEnemyUnits = new();

    /// <summary>
    /// 获取当前所有已生成的友方单位的只读集合。
    /// </summary>
    public IReadOnlyList<BattleUnit> SpawnedAllyUnits => _spawnedAllyUnits;

    /// <summary>
    /// 获取当前所有已生成的敌方单位的只读集合。
    /// </summary>
    public IReadOnlyList<BattleUnit> SpawnedEnemyUnits => _spawnedEnemyUnits;

    /// <summary>
    /// 当前敌方单位所使用的阵型模式（默认以线性阵型起步）。
    /// </summary>
    private EnemyLayoutFormation _currentFormation = EnemyLayoutFormation.Line;

    /// <summary>
    /// 根据传入的战斗数据载荷（BattleStartPayload），生成战斗双方的所有单位。
    /// </summary>
    /// <param name="payload">包含双方人数及阵型信息的战斗起始载荷。</param>
    public void SpawnAll(BattleStartPayload payload)
    {
        layout = FindAnyObjectByType<BattleFieldLayout>();

        // 创建并挂载双方单位的父节点
        allyRoot = new GameObject("Ally Root").transform;
        allyRoot.SetParent(layout.transform, false);

        enemyRoot = new GameObject("Enemy Root").transform;
        enemyRoot.SetParent(layout.transform, false);

        _currentFormation = payload.Formation;

        // 要先清空已生成的
        ClearAllUnits();

        // 生成单位并添加到对应的集合中
        _spawnedAllyUnits.AddRange(SpawnSide(payload.Allies.Count, true));
        _spawnedEnemyUnits.AddRange(SpawnSide(payload.Enemies.Count, false));
    }

    /// <summary>
    /// 按阵营生成一侧的多个单位，并记录他们的出生点与阵型归位点。
    /// </summary>
    /// <param name="count">该阵营要生成的单位数量。</param>
    /// <param name="isAlly">是否为友方阵营（true为友方，false为敌方）。</param>
    /// <returns>该阵营生成的所有 BattleUnit 列表。</returns>
    private List<BattleUnit> SpawnSide(int count, bool isAlly)
    {
        List<BattleUnit> units = new List<BattleUnit>();
        for (int i = 0; i < count; i++)
        {
            // 1. 计算最终战斗站位 (Home Position)
            Vector3 targetSlotPos = isAlly
                ? layout.GetAllySlotPos(i, count)
                : layout.GetEnemySlotPos(i, count, _currentFormation);

            // 2. 决定出生点 (Spawn Position)
            // 友方从统一入场点生成，敌方直接从最终站位生成（或者你也可以让敌人也有入场动画）
            Vector3 spawnPos = isAlly
                ? layout.initTrans.position
                : targetSlotPos;

            // 3. 生成单位
            BattleUnit unitObj = Instantiate(
                battleUnitPrefab,
                spawnPos,
                Quaternion.identity,
                isAlly ? allyRoot : enemyRoot
            );

            // 4. 记录缓存
            units.Add(unitObj);

            // [关键] 这里记录的是 targetSlotPos(最终站位), 不是 spawnPos(出生点).
            // 后续 BattleSetupState 会用这个 Home 位置来执行"跑进场"动画。
            _homePos[unitObj] = targetSlotPos;
        }

        return units;
    }

    /// <summary>
    /// 清理战场上所有已生成的单位，并重置记录的数据。
    /// </summary>
    public void ClearAllUnits()
    {
        // 清理友方单位
        foreach (var unit in _spawnedAllyUnits)
        {
            Destroy(unit.gameObject);
        }
        _spawnedAllyUnits.Clear();

        // 清理敌方单位
        foreach (var unit in _spawnedEnemyUnits)
        {
            Destroy(unit.gameObject);
        }
        _spawnedEnemyUnits.Clear();

        // 清空位置映射表
        _homePos.Clear();
    }
    /// <summary>
    /// 外部接口，拿到_homePos
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public Vector3 GetHomePos(BattleUnit unit) => _homePos[unit];
}
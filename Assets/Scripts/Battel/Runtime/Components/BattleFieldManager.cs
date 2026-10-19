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

    // 防止换位/击退动画尚未结束时再次触发阵型位移。
    private bool _formationMovementInProgress;

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

        // 先清空上一次生成的单位，再创建本次战斗的根节点。
        ClearAllUnits();

        allyRoot = new GameObject("Ally Root").transform;
        allyRoot.SetParent(layout.transform, false);

        enemyRoot = new GameObject("Enemy Root").transform;
        enemyRoot.SetParent(layout.transform, false);

        _currentFormation = payload.Formation;

        // 保留数据列表顺序，确保后续 RuntimeData 与 BattleUnit 按索引正确绑定。
        _spawnedAllyUnits.AddRange(SpawnSide(payload.Allies, true));
        _spawnedEnemyUnits.AddRange(SpawnSide(payload.Enemies, false));
    }

    /// <summary>
    /// 按阵营生成一侧的多个单位，并记录他们的出生点与阵型归位点。
    /// </summary>
    /// <param name="count">该阵营要生成的单位数量。</param>
    /// <param name="isAlly">是否为友方阵营（true为友方，false为敌方）。</param>
    /// <returns>该阵营生成的所有 BattleUnit 列表。</returns>
    private List<BattleUnit> SpawnSide(List<CharacterRuntimeData> members, bool isAlly)
    {
        List<BattleUnit> units = new List<BattleUnit>();
        for (int i = 0; i < members.Count; i++)
        {
            // 友方使用持久化的 Position；敌方继续沿用现有的阵型计算。
            Vector3 targetSlotPos = isAlly
                ? layout.GetAllySlotPos(members[i].Position)
                : layout.GetEnemySlotPos(i, members.Count, _currentFormation);

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
    /// 将我方单位移动到目标槽位，并把补位后的结果写回队伍数据。
    /// </summary>
    public bool MoveAllyToPosition(BattleEntity entity, int targetPosition, float duration = 0.35f)
    {
        if (entity == null || !entity.IsPlayer || layout == null || PartyManager.Instance == null)
            return false;

        if (!PartyManager.Instance.MoveMemberToPosition(entity.RuntimeData, targetPosition))
            return false;

        foreach (BattleUnit unit in _spawnedAllyUnits)
        {
            if (unit == null || unit.Entity == null)
                continue;

            Vector3 targetPos = layout.GetAllySlotPos(unit.Entity.RuntimeData.Position);
            _homePos[unit] = targetPos;
            StartCoroutine(unit.MoveToPosition(targetPos, duration));
        }

        return true;
    }

    /// <summary>
    /// 主动交换指定友军与目标位置上的队友。
    /// 距离限制由主动发起者的 CharacterDefinitionSO.maxSwapDistance 决定。
    /// 注意：当前项目尚无完整回合行动系统，本方法暂不扣除行动机会。
    /// </summary>
    public bool TrySwapAllyPositions(BattleEntity actor, int targetPosition, float duration = 0.35f)
    {
        if (_formationMovementInProgress || actor == null || !actor.IsPlayer ||
            actor.Unit == null || actor.RuntimeData == null ||
            PartyManager.Instance == null || layout == null ||
            !_spawnedAllyUnits.Contains(actor.Unit))
            return false;

        int currentPosition = actor.RuntimeData.Position;
        if (targetPosition < 1 || targetPosition > 4 || targetPosition == currentPosition)
            return false;

        int maxDistance = Mathf.Max(0, actor.Definition.maxSwapDistance);
        if (Mathf.Abs(targetPosition - currentPosition) > maxDistance)
        {
            Debug.Log($"换位失败：{actor.Definition.Name} 最多只能跨越 {maxDistance} 格。", actor.Unit);
            return false;
        }

        CharacterRuntimeData targetMember = PartyManager.Instance.GetMemberAtPosition(targetPosition);
        if (targetMember == null)
            return false;

        BattleUnit targetUnit = null;
        foreach (BattleUnit unit in _spawnedAllyUnits)
        {
            if (unit != null && unit.Entity != null && unit.Entity.RuntimeData == targetMember)
            {
                targetUnit = unit;
                break;
            }
        }

        if (targetUnit == null || targetUnit == actor.Unit)
            return false;

        // 数据层只交换两名角色的位置，其他队员不动。
        PartyManager.Instance.SwapPosition(currentPosition, targetPosition);

        Vector3 actorTarget = layout.GetAllySlotPos(targetPosition);
        Vector3 targetTarget = layout.GetAllySlotPos(currentPosition);
        _homePos[actor.Unit] = actorTarget;
        _homePos[targetUnit] = targetTarget;

        duration = Mathf.Max(0.01f, duration);
        _formationMovementInProgress = true;
        StartCoroutine(AnimateAllySwap(actor.Unit, targetUnit, actorTarget, targetTarget, duration));
        return true;
    }

    private System.Collections.IEnumerator AnimateAllySwap(
        BattleUnit actor, BattleUnit target, Vector3 actorTarget, Vector3 targetTarget, float duration)
    {
        StartCoroutine(actor.MoveToPosition(actorTarget, duration));
        StartCoroutine(target.MoveToPosition(targetTarget, duration));
        yield return new WaitForSeconds(duration);
        _formationMovementInProgress = false;
    }

    /// <summary>
    /// 将友军强制击退到更靠后的槽位。被击退者保持非跑步状态平滑位移，
    /// 因补位而改变位置的其他友军则播放正常移动动画。
    /// 注意：当前项目尚无完整回合行动系统，本方法不消耗行动机会。
    /// </summary>
    public bool TryKnockbackAllyToPosition(BattleEntity target, int targetPosition, float duration = 0.25f)
    {
        if (_formationMovementInProgress || target == null || !target.IsPlayer ||
            target.Unit == null || target.RuntimeData == null ||
            PartyManager.Instance == null || layout == null ||
            !_spawnedAllyUnits.Contains(target.Unit))
            return false;

        int oldPosition = target.RuntimeData.Position;
        if (targetPosition < 1 || targetPosition > 4 || targetPosition <= oldPosition)
            return false;

        if (!PartyManager.Instance.MoveMemberToPosition(target.RuntimeData, targetPosition))
            return false;

        duration = Mathf.Max(0.01f, duration);
        _formationMovementInProgress = true;
        StartCoroutine(AnimateAllyKnockback(target.Unit, duration));
        return true;
    }

    private System.Collections.IEnumerator AnimateAllyKnockback(BattleUnit knockedBackUnit, float duration)
    {
        foreach (BattleUnit unit in _spawnedAllyUnits)
        {
            if (unit == null || unit.Entity == null)
                continue;

            Vector3 targetPos = layout.GetAllySlotPos(unit.Entity.RuntimeData.Position);
            _homePos[unit] = targetPos;

            if (unit == knockedBackUnit)
            {
                StartCoroutine(unit.ForcedMoveToPosition(targetPos, duration));
            }
            else if ((unit.transform.position - targetPos).sqrMagnitude > 0.0001f)
            {
                // 只有实际需要补位的队友才播放跑步动画，原地不动的队友保持当前动画。
                StartCoroutine(unit.MoveToPosition(targetPos, duration));
            }
        }

        yield return new WaitForSeconds(duration);
        _formationMovementInProgress = false;
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
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战场管理器。
/// 负责实例化已完成站位分配的战斗单位、记录入场归位点，并管理友方换位和位移。
/// 敌方阵容的逻辑站位由 EnemySpawnManager 预先分配，本类不再决定敌人站在哪里。
/// </summary>
public class BattleFieldManager : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private BattleUnit battleUnitPrefab;

    private BattleFieldLayout layout;
    private Transform allyRoot;
    private Transform enemyRoot;

    private readonly Dictionary<BattleUnit, Vector3> _homePos = new();
    private bool _formationMovementInProgress;
    private readonly List<BattleUnit> _spawnedAllyUnits = new();
    private readonly List<BattleUnit> _spawnedEnemyUnits = new();

    public IReadOnlyList<BattleUnit> SpawnedAllyUnits => _spawnedAllyUnits;
    public IReadOnlyList<BattleUnit> SpawnedEnemyUnits => _spawnedEnemyUnits;

    /// <summary>
    /// 根据战斗起始载荷生成双方单位。
    /// </summary>
    public bool SpawnAll(BattleStartPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("BattleFieldManager：战斗载荷为空，无法生成战斗单位。", this);
            return false;
        }

        layout = FindAnyObjectByType<BattleFieldLayout>();
        if (layout == null)
        {
            Debug.LogError("BattleFieldManager：场景中找不到 BattleFieldLayout，无法生成战斗单位。", this);
            return false;
        }

        if (!EnemySpawnManager.ValidateAssignedFormation(payload.Enemies, out string formationError))
        {
            Debug.LogError($"BattleFieldManager：敌方站位无效，取消本次生成。{formationError}", this);
            return false;
        }

        ClearAllUnits();

        allyRoot = new GameObject("Ally Root").transform;
        allyRoot.SetParent(layout.transform, false);

        enemyRoot = new GameObject("Enemy Root").transform;
        enemyRoot.SetParent(layout.transform, false);

        // 保留数据列表顺序，确保 RuntimeData 与 BattleUnit 按索引正确绑定。
        _spawnedAllyUnits.AddRange(SpawnSide(payload.Allies, true));
        _spawnedEnemyUnits.AddRange(SpawnSide(payload.Enemies, false));
        return true;
    }

    /// <summary>
    /// 按阵营生成单位。友方使用队伍逻辑位置；敌方使用 EnemySpawnManager 已分配的位置。
    /// 大型敌人以连续占据槽位的坐标中心作为视觉中心。
    /// </summary>
    private List<BattleUnit> SpawnSide(List<CharacterRuntimeData> members, bool isAlly)
    {
        List<BattleUnit> units = new List<BattleUnit>();
        if (members == null)
            return units;

        for (int i = 0; i < members.Count; i++)
        {
            CharacterRuntimeData member = members[i];
            if (member == null || member.Definition == null)
            {
                Debug.LogError($"BattleFieldManager：第 {i + 1} 个单位缺少运行时数据或角色定义。", this);
                return new List<BattleUnit>();
            }

            Vector3 targetSlotPos;
            if (isAlly)
            {
                targetSlotPos = layout.GetAllySlotPos(member.Position);
            }
            else
            {
                EnemyDfinitionSo enemyDefinition = member.Definition as EnemyDfinitionSo;
                int occupiedCount = enemyDefinition != null
                    ? Mathf.Clamp(enemyDefinition.occupiedSlotCount, 1, 4)
                    : 1;

                // 位置已由 EnemySpawnManager 分配并在 SpawnAll 中验证，本类只负责坐标映射。
                targetSlotPos = layout.GetEnemySlotPos(member.Position, occupiedCount);
            }

            Vector3 spawnPos = isAlly ? layout.initTrans.position : targetSlotPos;
            BattleUnit unitObj = Instantiate(
                battleUnitPrefab,
                spawnPos,
                Quaternion.identity,
                isAlly ? allyRoot : enemyRoot
            );

            units.Add(unitObj);
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
                StartCoroutine(unit.ForcedMoveToPosition(targetPos, duration * 0.55f));
            }
            else if ((unit.transform.position - targetPos).sqrMagnitude > 0.0001f)
            {
                StartCoroutine(unit.MoveToPosition(targetPos, duration));
            }
        }

        yield return new WaitForSeconds(duration);
        _formationMovementInProgress = false;
    }

    /// <summary>
    /// 供即时技能调用的站位操作。前进/后退/击退都改变逻辑站位，场地管理器负责同步视觉位置。
    /// delta 为负数表示前进，正数表示后退。
    /// </summary>
    public bool TryMoveEntityBySlots(BattleEntity target, int delta, bool knockback = false, float duration = 0.3f)
    {
        if (_formationMovementInProgress || target == null || target.Unit == null ||
            target.RuntimeData == null || delta == 0 || layout == null)
            return false;

        int oldPosition = target.RuntimeData.Position;
        if (oldPosition < 1 || oldPosition > 4)
            return false;

        int targetPosition = Mathf.Clamp(oldPosition + delta, 1, 4);
        if (targetPosition == oldPosition)
            return false;

        if (target.IsPlayer)
        {
            if (knockback)
                return targetPosition > oldPosition &&
                       TryKnockbackAllyToPosition(target, targetPosition, duration);
            return MoveAllyToPosition(target, targetPosition, duration);
        }

        EnemyDfinitionSo definition = target.Definition as EnemyDfinitionSo;
        int occupiedCount = definition != null ? Mathf.Clamp(definition.occupiedSlotCount, 1, 4) : 1;
        int maxStartPosition = 5 - occupiedCount;
        targetPosition = Mathf.Clamp(targetPosition, 1, maxStartPosition);
        if (targetPosition == oldPosition || !IsEnemyPositionAvailable(target, targetPosition, occupiedCount))
            return false;

        target.RuntimeData.Position = targetPosition;
        Vector3 destination = layout.GetEnemySlotPos(targetPosition, occupiedCount);
        _homePos[target.Unit] = destination;
        _formationMovementInProgress = true;
        StartCoroutine(AnimateEnemyMove(target.Unit, destination, Mathf.Max(0.01f, duration), knockback));
        return true;
    }

    /// <summary>交换同阵营的两个实体。敌方只允许相同占格尺寸互换，以免造成槽位重叠。</summary>
    public bool TrySwapEntities(BattleEntity actor, BattleEntity target, float duration = 0.35f)
    {
        if (_formationMovementInProgress || actor == null || target == null ||
            actor == target || actor.IsPlayer != target.IsPlayer ||
            actor.RuntimeData == null || target.RuntimeData == null ||
            actor.Unit == null || target.Unit == null || layout == null)
            return false;

        if (actor.IsPlayer)
            return TrySwapAllyPositions(actor, target.RuntimeData.Position, duration);

        EnemyDfinitionSo actorDefinition = actor.Definition as EnemyDfinitionSo;
        EnemyDfinitionSo targetDefinition = target.Definition as EnemyDfinitionSo;
        int actorSize = actorDefinition != null ? Mathf.Clamp(actorDefinition.occupiedSlotCount, 1, 4) : 1;
        int targetSize = targetDefinition != null ? Mathf.Clamp(targetDefinition.occupiedSlotCount, 1, 4) : 1;
        if (actorSize != targetSize)
            return false;

        int actorPosition = actor.RuntimeData.Position;
        int targetPosition = target.RuntimeData.Position;
        if (actorPosition < 1 || targetPosition < 1 || actorPosition == targetPosition)
            return false;

        actor.RuntimeData.Position = targetPosition;
        target.RuntimeData.Position = actorPosition;
        Vector3 actorDestination = layout.GetEnemySlotPos(targetPosition, actorSize);
        Vector3 targetDestination = layout.GetEnemySlotPos(actorPosition, targetSize);
        _homePos[actor.Unit] = actorDestination;
        _homePos[target.Unit] = targetDestination;
        _formationMovementInProgress = true;
        StartCoroutine(AnimateEnemySwap(actor.Unit, target.Unit, actorDestination, targetDestination, Mathf.Max(0.01f, duration)));
        return true;
    }

    private bool IsEnemyPositionAvailable(BattleEntity movingEntity, int position, int size)
    {
        int end = position + size - 1;
        foreach (BattleUnit unit in _spawnedEnemyUnits)
        {
            if (unit == null || unit.Entity == null || unit.Entity == movingEntity ||
                !unit.Entity.IsAlive || unit.Entity.RuntimeData == null)
                continue;

            EnemyDfinitionSo otherDefinition = unit.Entity.Definition as EnemyDfinitionSo;
            int otherSize = otherDefinition != null ? Mathf.Clamp(otherDefinition.occupiedSlotCount, 1, 4) : 1;
            int otherStart = unit.Entity.RuntimeData.Position;
            int otherEnd = otherStart + otherSize - 1;
            if (position <= otherEnd && otherStart <= end)
                return false;
        }

        return position >= 1 && end <= 4;
    }

    private System.Collections.IEnumerator AnimateEnemyMove(BattleUnit unit, Vector3 destination, float duration, bool knockback)
    {
        if (unit != null)
        {
            if (knockback)
                yield return StartCoroutine(unit.ForcedMoveToPosition(destination, duration));
            else
                yield return StartCoroutine(unit.MoveToPosition(destination, duration));
        }

        _formationMovementInProgress = false;
    }

    private System.Collections.IEnumerator AnimateEnemySwap(
        BattleUnit actor, BattleUnit target, Vector3 actorDestination, Vector3 targetDestination, float duration)
    {
        StartCoroutine(actor.MoveToPosition(actorDestination, duration));
        StartCoroutine(target.MoveToPosition(targetDestination, duration));
        yield return new WaitForSeconds(duration);
        _formationMovementInProgress = false;
    }

    /// <summary>
    /// 清理战场上所有已生成的单位，并重置记录的数据。
    /// </summary>
    public void ClearAllUnits()
    {
        foreach (var unit in _spawnedAllyUnits)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        _spawnedAllyUnits.Clear();

        foreach (var unit in _spawnedEnemyUnits)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        _spawnedEnemyUnits.Clear();

        _homePos.Clear();

        if (allyRoot != null)
            Destroy(allyRoot.gameObject);
        if (enemyRoot != null)
            Destroy(enemyRoot.gameObject);

        allyRoot = null;
        enemyRoot = null;
    }

    /// <summary>
    /// 外部接口，拿到 _homePos。
    /// </summary>
    public Vector3 GetHomePos(BattleUnit unit) => _homePos[unit];
}

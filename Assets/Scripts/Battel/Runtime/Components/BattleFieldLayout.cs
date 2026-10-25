using UnityEngine;

/// <summary>
/// 战场布局配置类，管理友方和敌方逻辑槽位到场景坐标的映射。
/// 逻辑编号与屏幕左右顺序分离：敌方视觉上为 4、3、2、1，友方为 1、2、3、4。
/// 双方的逻辑 1、2 号位均为前排，3、4 号位均为后排；每个槽位只能被一个占格实体占用。
/// </summary>
public class BattleFieldLayout : MonoBehaviour
{
    [Header("Scene References")]
    /// <summary>
    /// 行动者站位 (红色 Action Position)，轮到行动者时移动到这里。
    /// </summary>
    public Transform actionTrans;

    /// <summary>
    /// 初始站位。
    /// </summary>
    public Transform initTrans;

    [Header("Ally Fixed Slots")]
    [SerializeField] private Transform allySlot1;
    [SerializeField] private Transform allySlot2;
    [SerializeField] private Transform allySlot3;
    [SerializeField] private Transform allySlot4;

    [Header("Enemy Fixed Slots")]
    [SerializeField] private Transform enemySlot1;
    [SerializeField] private Transform enemySlot2;
    [SerializeField] private Transform enemySlot3;
    [SerializeField] private Transform enemySlot4;

    [Min(0f)]
    [SerializeField, Tooltip("同一逻辑位置存在多个普通敌人时，沿 Y 轴分散的间距。")]
    private float enemyGroupVerticalSpacing = 0.55f;

    [Header("Legacy Enemy Formation Anchors")]
    [Tooltip("旧版动态阵型锚点。新固定槽位系统不再使用，验证完成后再从场景中清理。")]
    [SerializeField] private Transform enemyTopTrans;
    [SerializeField] private Transform enemyBottomTrans;
    [SerializeField] private Transform bossCenterTrans;
    [SerializeField] private Transform minionTopTrans;
    [SerializeField] private Transform minionBottomTrans;

    /// <summary>
    /// 根据逻辑位置获取友方单位的固定站位。位置编号为 1~4，与队伍人数无关。
    /// </summary>
    public Vector3 GetAllySlotPos(int position)
    {
        Transform slot = GetAllySlotTransform(position);
        if (slot == null)
        {
            Debug.LogError($"BattleFieldLayout 尚未配置 AllySlot{position}。", this);
            return transform.position;
        }

        return slot.position;
    }

    /// <summary>
    /// 获取敌方固定槽位坐标。
    /// occupiedSlotCount 大于 1 时，取连续占用槽位的世界坐标平均值，作为大型敌人的视觉中心。
    /// position 表示该敌人占用的起始逻辑位置。
    /// </summary>
    public Vector3 GetEnemySlotPos(int position, int occupiedSlotCount = 1)
    {
        occupiedSlotCount = Mathf.Clamp(occupiedSlotCount, 1, 4);
        int safePosition = Mathf.Clamp(position, 1, 5 - occupiedSlotCount);

        if (safePosition != position)
        {
            Debug.LogWarning(
                $"敌人起始位置 {position} 与占位数量 {occupiedSlotCount} 超出 1~4 槽位范围，已临时调整为 {safePosition}。",
                this);
        }

        Vector3 totalPosition = Vector3.zero;
        for (int i = 0; i < occupiedSlotCount; i++)
        {
            int currentPosition = safePosition + i;
            Transform slot = GetEnemySlotTransform(currentPosition);

            if (slot == null)
            {
                Debug.LogError($"BattleFieldLayout 尚未配置 EnemySlot{currentPosition}。", this);
                return transform.position;
            }

            totalPosition += slot.position;
        }

        return totalPosition / occupiedSlotCount;
    }

    /// <summary>
    /// 旧版视觉偏移辅助方法。新生成器会禁止敌人占格冲突，因此新流程不调用此方法。
    /// </summary>
    public Vector3 GetEnemyVisualOffset(int indexInGroup, int groupCount)
    {
        if (groupCount <= 1)
            return Vector3.zero;

        float centeredIndex = indexInGroup - (groupCount - 1) * 0.5f;
        return new Vector3(0f, centeredIndex * enemyGroupVerticalSpacing, 0f);
    }

    private Transform GetAllySlotTransform(int position)
    {
        return position switch
        {
            1 => allySlot1,
            2 => allySlot2,
            3 => allySlot3,
            4 => allySlot4,
            _ => null
        };
    }

    private Transform GetEnemySlotTransform(int position)
    {
        return position switch
        {
            1 => enemySlot1,
            2 => enemySlot2,
            3 => enemySlot3,
            4 => enemySlot4,
            _ => null
        };
    }

    #region Legacy API
    /// <summary>
    /// 旧版动态阵型接口。仅保留以避免迁移阶段其他旧调用立即失效，新系统不应继续使用。
    /// </summary>
    public Vector3 GetEnemySlotPos(int index, int count, EnemyLayoutFormation formation)
    {
        switch (formation)
        {
            case EnemyLayoutFormation.Line:
                return LerpByMidpointRule(enemyTopTrans.position, enemyBottomTrans.position, index, count);
            case EnemyLayoutFormation.BossTriangle:
                return GetEnemyBossTrianglePos(index, count);
            default:
                return Vector3.zero;
        }
    }

    private Vector3 LerpByMidpointRule(Vector3 start, Vector3 end, int index, int count)
    {
        if (count <= 0)
            return start;

        float t = (index + 0.5f) / count;
        return Vector3.Lerp(start, end, t);
    }

    private Vector3 GetEnemyBossTrianglePos(int index, int count)
    {
        if (index == 0)
            return bossCenterTrans.position;

        return LerpByMidpointRule(minionTopTrans.position, minionBottomTrans.position, index, count);
    }
    #endregion
}



/// <summary>
/// 战场布局配置类，管理友方和敌方单位在场景中的站位及阵型逻辑。
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

    [Header("Enemy Slot Segment (Normal)")]
    /// <summary>
    /// 敌方普通阵型站位线段起点（顶部）。
    /// </summary>
    [SerializeField] private Transform enemyTopTrans;

    /// <summary>
    /// 敌方普通阵型站位线段终点（底部）。
    /// </summary>
    [SerializeField] private Transform enemyBottomTrans;

    [Header("Enemy Formation (BossTriangle)")]
    /// <summary>
    /// BOSS 站位（三角形阵型中心点）。
    /// </summary>
    [SerializeField] private Transform bossCenterTrans;

    /// <summary>
    /// BOSS 三角形阵型中，小怪的站位线段起点（顶部）。
    /// </summary>
    [SerializeField] private Transform minionTopTrans;

    /// <summary>
    /// BOSS 三角形阵型中，小怪的站位线段终点（底部）。
    /// </summary>
    [SerializeField] private Transform minionBottomTrans;

    /// <summary>
    /// 根据逻辑位置获取友方单位的固定站位。位置编号为 1~4，与队伍人数无关。
    /// </summary>
    /// <param name="position">角色的逻辑位置（1~4）。</param>
    /// <returns>对应槽位的世界坐标。</returns>
    public Vector3 GetAllySlotPos(int position)
    {
        Transform slot = null;

        switch (position)
        {
            case 1: slot = allySlot1; break;
            case 2: slot = allySlot2; break;
            case 3: slot = allySlot3; break;
            case 4: slot = allySlot4; break;
            default:
                Debug.LogError($"友方站位编号无效：{position}，有效范围为 1~4。", this);
                return transform.position;
        }

        if (slot == null)
        {
            Debug.LogError($"BattleFieldLayout 尚未配置 AllySlot{position}。", this);
            return transform.position;
        }

        return slot.position;
    }

    /// <summary>
    /// 获取敌方单位的目标站立位置，根据阵型不同计算方式不同。
    /// </summary>
    /// <param name="index">单位在队列中的索引（0开始）。</param>
    /// <param name="count">队列总人数。</param>
    /// <param name="formation">指定的敌方阵型枚举。</param>
    /// <returns>计算后的世界坐标。</returns>
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

    #region 线段等分算法
    /// <summary>
    /// 线段等分算法：计算线段 [0, 1] 区间内，排除两端点后的等分点位置。
    /// </summary>
    /// <param name="start">线段起点。</param>
    /// <param name="end">线段终点。</param>
    /// <param name="index">第几个等分点。</param>
    /// <param name="count">总分段数。</param>
    /// <returns>计算后的世界坐标。</returns>
    private Vector3 LerpByMidpointRule(Vector3 start, Vector3 end, int index, int count)
    {
        float t = (index + 0.5f) / count;
        return Vector3.Lerp(start, end, t);
    }

    /// <summary>
    /// 获取 BOSS 三角形阵型下的具体站立位置。
    /// </summary>
    /// <param name="index">单位在队列中的索引（0开始）。</param>
    /// <param name="count">队列总人数。</param>
    /// <returns>计算后的世界坐标。</returns>
    private Vector3 GetEnemyBossTrianglePos(int index, int count)
    {
        if (index == 0)
            return bossCenterTrans.position;
        return LerpByMidpointRule(minionTopTrans.position, minionBottomTrans.position, index, count);
    }
    #endregion
}
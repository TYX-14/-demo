
/// <summary>
/// 战斗实体的运行时包装类。
/// 作为战斗核心的数据载体，关联了角色的运行时数据、场景中的视觉组件（BattleUnit）以及阵营身份标识。
/// </summary>
public class BattleEntity
{
    /// <summary>
    /// 该战斗实体对应的运行时角色数据。
    /// </summary>
    public CharacterRuntimeData RuntimeData { get; }

    /// <summary>
    /// 获取角色的基础定义配置（由 RuntimeData 代理提供）。
    /// </summary>
    public CharacterDefinitionSO Definition => RuntimeData.Definition;

    /// <summary>
    /// 该实体在战斗场景中对应的视觉控制组件（BattleUnit）。
    /// </summary>
    public BattleUnit Unit { get; }

    /// <summary>
    /// 该实体的稳定唯一标识符。
    /// </summary>
    public string ID { get; }

    /// <summary>
    /// 标记该实体是否属于玩家阵营。
    /// </summary>
    public bool IsPlayer { get; }

    /// <summary>
    /// 判断实体当前是否存活。
    /// </summary>
    public bool IsAlive => RuntimeData.CurrentHP > 0;

    /// <summary>
    /// 获取该实体当前的生命值（由 RuntimeData 代理提供）。
    /// </summary>
    public int CurrentHP => RuntimeData.CurrentHP;

    /// <summary>
    /// 获取该实体计算后的总属性面板（由 RuntimeData 代理提供）。
    /// </summary>
    public StatBlock TotalStats => RuntimeData.GetTotalStats();

    /// <summary>
    /// 初始化战斗实体数据类的新实例。
    /// </summary>
    /// <param name="runtimeData">实体的运行时数据对象。</param>
    /// <param name="unit">实体的场景节点（BattleUnit 组件）。</param>
    /// <param name="isPlayer">标记是否为玩家单位。</param>
    /// <param name="stableID">实体的稳定 ID。</param>
    public BattleEntity(CharacterRuntimeData runtimeData, BattleUnit unit, bool isPlayer, string stableID)
    {
        RuntimeData = runtimeData;
        Unit = unit;
        IsPlayer = isPlayer;
        ID = stableID;
    }
}


/// <summary>
/// 战斗开始时传递的数据包。
/// 用于将参战双方以及敌人阵型信息
/// 一次性传递给战斗系统。
/// </summary>
public class BattleStartPayload 
{
    /// <summary>
    /// 我方参战角色列表
    /// </summary>
    public List<CharacterRuntimeData> Allies { get; }

    /// <summary>
    /// 敌方参战角色列表
    /// </summary>
    public List<CharacterRuntimeData> Enemies { get; }

    /// <summary>
    /// 敌人的阵型配置
    /// </summary>
    public EnemyLayoutFormation Formation { get; }

    /// <summary>
    /// 构造战斗启动数据
    /// </summary>
    /// <param name="allies">我方角色列表</param>
    /// <param name="enemies">敌方角色列表</param>
    /// <param name="formation">敌人阵型</param>
    public BattleStartPayload(
        List<CharacterRuntimeData> allies,
        List<CharacterRuntimeData> enemies,
        EnemyLayoutFormation formation)
    {
        // 保存我方参战单位
        Allies = allies;

        // 保存敌方参战单位
        Enemies = enemies;

        // 保存阵型信息
        Formation = formation;
    }
}

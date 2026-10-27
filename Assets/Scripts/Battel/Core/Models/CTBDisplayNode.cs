using UnityEngine;

/// <summary>
/// CTB 时间轴节点的最小展示数据。
/// 调度器把真实行动顺序转换为此结构，UI 不需要自行读取角色定义或推断阵营。
/// </summary>
public readonly struct CTBDisplayNode
{
    public BattleEntity Entity { get; }
    public string EntityId { get; }
    public Sprite BattleIdleSprite { get; }
    public bool IsPlayer { get; }

    public CTBDisplayNode(BattleEntity entity)
    {
        Entity = entity;
        EntityId = entity != null ? entity.ID : string.Empty;
        BattleIdleSprite = entity != null && entity.Definition != null
            ? entity.Definition.LeftBattleIdle
            : null;
        IsPlayer = entity != null && entity.IsPlayer;
    }

    public static CTBDisplayNode[] FromEntities(System.Collections.Generic.IEnumerable<BattleEntity> entities)
    {
        if (entities == null)
            return System.Array.Empty<CTBDisplayNode>();

        var nodes = new System.Collections.Generic.List<CTBDisplayNode>();
        foreach (BattleEntity entity in entities)
        {
            if (entity != null && entity.IsAlive)
                nodes.Add(new CTBDisplayNode(entity));
        }

        return nodes.ToArray();
    }
}

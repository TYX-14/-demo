using MFrameork.Event;
using UnityEngine; // 包含 Debug
using System.Collections; // 包含 IEnumerator

/// <summary>
/// 战斗初始设置状态。
/// 属于战斗状态机的一部分，负责在战斗正式开始前载入初始的数据载荷。
/// 当前代码中仅用日志占位，后续需在此阶段完成生成参战单位等初始化工作。
/// </summary>
public class BattleSetupState : BattleState
{
    /// <summary>
    /// 战斗开始的数据载荷。
    /// 包含参与战斗的双方阵容、配置等起步数据。
    /// </summary>
    private readonly BattleStartPayload _startPayload;

    /// <summary>
    /// 初始化战斗设置状态。
    /// </summary>
    /// <param name="controller">战斗控制器实例。</param>
    /// <param name="startPayload">用于启动战斗的载荷数据。</param>
    public BattleSetupState(BattleController controller, BattleStartPayload startPayload) : base(controller)
    {
        _startPayload = startPayload;
    }

    /// <summary>
    /// 执行战斗准备状态的核心逻辑。
    /// 1. 调用战场管理器生成敌我双方的 3D 视觉单位 (BattleUnit)。
    /// 2. 将运行时数据 (RuntimeData)、视觉单位 (BattleUnit) 与战斗实体逻辑 (BattleEntity) 进行正式绑定。
    /// 3. 当前处于演示阶段，执行完后立即结束协程。
    /// </summary>
    public override IEnumerator Execute()
    {
        // 1. 先按 payload 在战斗场景里生成敌我双方的 BattleUnit。
        _controller.FieldManager.SpawnAll(_startPayload);

        // 2. 再把 RuntimeData、BattleUnit 和 BattleEntity 正式绑起来。
        _controller.AllEntities.Clear();
        var allyEntities = CreateEntities(_startPayload.Allies, _controller.FieldManager.SpawnedAllyUnits, true);
        _controller.AllEntities.AddRange(allyEntities);
        _controller.AllEntities.AddRange(CreateEntities(_startPayload.Enemies, _controller.FieldManager.SpawnedEnemyUnits, false));

        //3.战斗开始时，友方跑位
        float runtime = 1f;//TODO 以后统一管理
        if (runtime > 0f)
        {
            yield return new WaitForSeconds(runtime);

            foreach (var entity in allyEntities)
            {
                Vector3 homePos = _controller.FieldManager.GetHomePos(entity.Unit);
                _controller.StartCoroutine(entity.Unit.MoveToPosition(homePos,runtime));
            }
            yield return new WaitForSeconds(runtime);
        }

        // 友军完成入场跑位后，通知订阅者战斗正式开始。
        EventBus.Publish(new BattleStartedEvent());

        //TODO 演示效果
        _controller.StopBattle();
        yield break;
    }

    /// <summary>
    /// 创建特定阵营的战斗实体集合。
    /// 遍历运行时数据列表，将对应的数据、游戏物体和实例化出的单位一一对应并绑定，生成唯一识别 ID。
    /// </summary>
    /// <param name="runtimeList">该阵营的角色运行时数据列表。</param>
    /// <param name="units">该阵营已经生成在场景中的 BattleUnit 列表。</param>
    /// <param name="isPlayer">标记是否为玩家阵营 (Player / Enemy)。</param>
    /// <returns>绑定完成的 BattleEntity 集合。</returns>
    private List<BattleEntity> CreateEntities(List<CharacterRuntimeData> runtimeList, IReadOnlyList<BattleUnit> units, bool isPlayer)
    {
        // 【关键】预分配列表容量，直接填入 runtimeList 的数量。
        // 这样做能有效避免 List 在动态增加元素时多次触发底层数组扩容，节省性能开销。
        var entities = new List<BattleEntity>(runtimeList.Count);

        // 根据阵营决定唯一 ID 的前缀，P 代表 Player (玩家)，E 代表 Enemy (敌人)
        string side = isPlayer ? "P" : "E";

        for (var i = 0; i < runtimeList.Count; i++)
        {
            // 【关键】通过下标 i 强制对齐数据与场景物体。
            // 这种写法强依赖 List 的顺序必须严格一致。
            var runtimeData = runtimeList[i]; // 取第 i 个角色的数值数据（血量、攻击力等）
            BattleUnit unit = units[i];       // 取第 i 个角色在场景里的预制体（控制动画、光标等）

            // 【核心】生成唯一实体 ID，格式如：P_00_战士ID。
            // 格式 [阵营]_[两位索引]_[配置ID]，这种结构在后期日志排错非常直观。
            var entity = new BattleEntity(runtimeData, unit, isPlayer, $"{side}_{i:D2}_{runtimeData.Definition.ID}");

            // 【关键】双向绑定：
            // 逻辑层(BattleEntity)拥有了对应场景物体(unit)，而场景物体(unit)也必须拥有逻辑层。
            // 这样，当逻辑层扣血时，能立刻调用 unit 播放受伤动画；同样，如果场景物体死了，也能通知逻辑层状态更新。
            unit.Bind(entity);

            entities.Add(entity);
        }

        return entities;
    }
}
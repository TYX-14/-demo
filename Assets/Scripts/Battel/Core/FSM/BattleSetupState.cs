using MFrameork.Event;
using UnityEngine; // 包含 Debug
using System.Collections; // 包含 IEnumerator

/// <summary>
/// 战斗初始设置状态。
/// 负责生成并绑定敌我双方的战斗实体、完成友军入场跑位，随后将控制权交给下一行动者选择状态。
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
    /// 1. 生成敌我双方的战斗单位。
    /// 2. 将运行时数据、视觉单位与战斗实体绑定。
    /// 3. 等待友军完成入场跑位。
    /// 4. 发布战斗开始事件，并切换到下一行动者选择状态。
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

        // 3. 战斗开始时，友方跑位。
        float runtime = 1f; // TODO：以后统一管理
        if (runtime > 0f)
        {
            yield return new WaitForSeconds(runtime);

            foreach (var entity in allyEntities)
            {
                Vector3 homePos = _controller.FieldManager.GetHomePos(entity.Unit);
                _controller.StartCoroutine(entity.Unit.MoveToPosition(homePos, runtime));
            }

            yield return new WaitForSeconds(runtime);
        }

        // 4. 通知 HUD 等订阅者：友军已完成入场，战斗正式开始。
        EventBus.Publish(new BattleStartedEvent());

        // 5. 将状态机控制权交给回合分发器，开始选择下一位行动者。
        _controller.SetState(new SetNextEntityState(_controller));
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
        // 预分配列表容量，减少 List 动态扩容。
        var entities = new List<BattleEntity>(runtimeList.Count);

        // 根据阵营决定唯一 ID 的前缀，P 代表 Player，E 代表 Enemy。
        string side = isPlayer ? "P" : "E";

        for (var i = 0; i < runtimeList.Count; i++)
        {
            // 通过下标对齐运行时数据与场景物体。
            var runtimeData = runtimeList[i];
            BattleUnit unit = units[i];

            // 生成唯一实体 ID，格式如 P_00_角色ID。
            var entity = new BattleEntity(runtimeData, unit, isPlayer, $"{side}_{i:D2}_{runtimeData.Definition.ID}");

            // 双向绑定逻辑实体与视觉单位。
            unit.Bind(entity);
            entities.Add(entity);
        }

        return entities;
    }
}

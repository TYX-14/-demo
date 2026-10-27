using MFrameork.Event;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 战斗初始设置状态。
/// 负责生成并绑定敌我双方的战斗实体、完成友军入场跑位，随后将控制权交给下一行动者选择状态。
/// </summary>
public class BattleSetupState : BattleState
{
    private readonly BattleStartPayload _startPayload;

    public BattleSetupState(BattleController controller, BattleStartPayload startPayload) : base(controller)
    {
        _startPayload = startPayload;
    }

    public override IEnumerator Execute()
    {
        // 1. 先按 payload 在战斗场景里生成敌我双方的 BattleUnit。
        if (!_controller.FieldManager.SpawnAll(_startPayload))
        {
            _controller.StopBattle();
            yield break;
        }

        // 2. 再把 RuntimeData、BattleUnit 和 BattleEntity 正式绑起来。
        _controller.AllEntities.Clear();
        var allyEntities = CreateEntities(_startPayload.Allies, _controller.FieldManager.SpawnedAllyUnits, true);
        _controller.AllEntities.AddRange(allyEntities);
        _controller.AllEntities.AddRange(CreateEntities(_startPayload.Enemies, _controller.FieldManager.SpawnedEnemyUnits, false));

        // 只准备行动顺序，不在跑位完成前选择正式行动者。
        _controller.InitializeRoundScheduler();

        // 保留现有的短暂开场停顿，然后启动所有友军跑位。
        float runtime = 1f; // TODO：以后统一管理
        if (runtime > 0f)
        {
            yield return new WaitForSeconds(runtime);

            var movementFinished = new bool[allyEntities.Count];
            for (int i = 0; i < allyEntities.Count; i++)
            {
                int index = i;
                BattleEntity entity = allyEntities[index];
                Vector3 homePos = _controller.FieldManager.GetHomePos(entity.Unit);
                _controller.StartCoroutine(MoveUnitAndSignal(entity, homePos, runtime, movementFinished, index));
            }

            // 等待每个移动协程真实结束，而不是假定固定等待时间就一定跑位完成。
            bool allFinished;
            do
            {
                allFinished = true;
                for (int i = 0; i < movementFinished.Length; i++)
                {
                    if (movementFinished[i])
                        continue;
                    allFinished = false;
                    break;
                }

                if (!allFinished)
                    yield return null;
            }
            while (!allFinished);
        }

        // 跑位全部完成后才通知 HUD 显示；随后才选择第一名正式行动者。
        EventBus.Publish(new BattleStartedEvent());
        _controller.SetState(new SetNextEntityState(_controller));
        yield break;
    }

    private IEnumerator MoveUnitAndSignal(
        BattleEntity entity,
        Vector3 homePosition,
        float duration,
        bool[] completed,
        int index)
    {
        if (entity != null && entity.Unit != null)
            yield return entity.Unit.MoveToPosition(homePosition, duration);

        completed[index] = true;
    }

    private List<BattleEntity> CreateEntities(
        List<CharacterRuntimeData> runtimeList,
        IReadOnlyList<BattleUnit> units,
        bool isPlayer)
    {
        var entities = new List<BattleEntity>(runtimeList.Count);
        string side = isPlayer ? "P" : "E";

        for (var i = 0; i < runtimeList.Count; i++)
        {
            var runtimeData = runtimeList[i];
            BattleUnit unit = units[i];
            var entity = new BattleEntity(
                runtimeData,
                unit,
                isPlayer,
                $"{side}_{i:D2}_{runtimeData.Definition.ID}");

            unit.Bind(entity);
            entities.Add(entity);
        }

        return entities;
    }
}



using UnityEngine.AddressableAssets;
using System.Collections.Generic;

/// <summary>
/// 战斗服务核心类：负责战斗的启动、场景切换、以及战斗前状态整理
/// </summary>
public class BattleService : singleton<BattleService>
{
    /// <summary>战斗结束后返回的场景引用</summary>
    private AssetReference _returnSceneAfterBattle;

    /// <summary>战斗启动缓存数据</summary>
    private BattleStartPayload _pendingPayload;

    /// <summary>是否存在待处理的战斗数据</summary>
    public bool HasPendingPayload => _pendingPayload != null;

    /// <summary>
    /// 外部拿到_pendingPayload（战斗启动缓存数据），拿到之后清除_pendingPayload缓存数据
    /// </summary>
    /// <returns></returns>
    public BattleStartPayload ConsumeStartPayload()
    {
        var payload = _pendingPayload;
        _pendingPayload = null;
        return payload;
    }

    /// <summary>
    /// 从关卡挑战行为触发战斗
    /// </summary>
    public void StartBattleFromAction(ChallengeAction action)
    {
        List<CharacterRuntimeData> allies =
            new List<CharacterRuntimeData>(PartyManager.Instance.PartyMembers);

        List<CharacterRuntimeData> enemies = new List<CharacterRuntimeData>();

        foreach (var definition in action.npcTeamMembers)
        {
            if (definition is not null)
                enemies.Add(new CharacterRuntimeData(definition));
        }

        StartBattle(
            allies,
            enemies,
            action.battleSceneReference,
            action.enemyLayoutFormation
        );
    }

    /// <summary>
    /// 正式启动战斗流程（核心入口）
    /// </summary>
    public void StartBattle(
        List<CharacterRuntimeData> allies,
        List<CharacterRuntimeData> enemies,
        AssetReference battleScene,
        EnemyLayoutFormation formation)
    {
        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;

        // 记录返回场景（战斗结束后回到哪里）
        _returnSceneAfterBattle = sceneLoadManager.activeScene;

        // 战斗前状态归一化（避免残血/异常状态进入战斗）
        NormalizeBattleSessionState(PartyManager.Instance.PartyMembers);

        // 组装战斗加载数据
        _pendingPayload = new BattleStartPayload(
            new List<CharacterRuntimeData>(allies),
            new List<CharacterRuntimeData>(enemies),
            formation
        );

        // 切换到战斗场景
        sceneLoadManager.RequestLoad(
            new SceneLoadRequest(
                battleScene,
                FadeStyle.WipeMask,
                GameMode.Battle
            )
        );
    }

    /// <summary>
    /// 战斗开始前统一修正角色状态
    /// </summary>
    private void NormalizeBattleSessionState(List<CharacterRuntimeData> members)
    {
        //如果有角色死了，再次进入战斗时应该自动变成1血
        foreach (var member in members)
        {
            member.ResetBattleBP();

            if (member.CurrentHP <= 0)
                member.CurrentHP = 1;
        }
    }
}
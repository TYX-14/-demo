using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class ChallengeAction : ActionBase
{
    
    [Header("Challenge Action")]

    // 战斗场景的资源引用（通常配合 Addressables 资源管理系统使用，实现异步加载）
    public AssetReference battleSceneReference;

    // 参与挑战的 NPC 队友列表，存储的是 ScriptableObject (SO) 类型的角色定义
    public List<CharacterDefinitionSO> npcTeamMembers;

    [Header("可选遭遇配置")]
    [Tooltip("指定后由遭遇配置完整定义本场敌人及站位；不指定时，使用 npcTeamMembers 自动布阵。")]
    public BattleEncounterSO encounterConfiguration;

     [Header("阵型")]

    // 敌人的布局阵型，默认设置为一字排开的线型阵（EnemyLayoutFormation.Line）
    public EnemyLayoutFormation enemyLayoutFormation = EnemyLayoutFormation.Line;

    // 属性：当前与玩家交互的 NPC 或怪物角色定义，外部可读，内部私有设置
    public CharacterDefinitionSO CurrentCharacter {  get; private set; }

    // 属性：上一次选择或挑战的难度系数
    public int LastDifficulty { get; private set; }


    
    private void Awake()
    {
        // 获取当前游戏对象上的 CharacterIdentity 组件，并拿到它身上绑定的角色定义数据
        CurrentCharacter = GetComponent<CharacterIdentity>().CharacterDefinition;
        

        // 空合并赋值：如果 npcTeamMembers 列表为 null，则初始化一个空列表，防止报空指针异常
        npcTeamMembers ??= new List<CharacterDefinitionSO>();

        // 检查队友列表中是否已经包含了当前角色
        if (!npcTeamMembers.Contains(CurrentCharacter))
        {
            // 如果不包含，则将当前角色插入到队伍列表的最前端（索引 0 的位置）
            npcTeamMembers.Insert(0, CurrentCharacter);
        }
    }

    public override void Execute(object contextData = null)
    {
        BattleService.Instance.StartBattleFromAction(this);
    }

    // 重写触发行为的方法，当玩家与该物体交互（例如点击或走近按 E）时调用
    // 传入参数 interactor 是触发这次交互的盟友/玩家定义
    public override void TriggerAction(AllyDefinitionSO interactor)
    {
        LastDifficulty = EvaluateDifficulty(EvaluatePlayerTeamPower(), EvaluateEnemyTeamPower());


        // 通过事件总线（EventBus）发布一个打开面板的请求事件，并将当前脚本实例（this）作为参数传过去
        // UI 层收到这个事件后，就会根据这里面的数据（如场景、队友、阵型等）渲染出上一张图里的 UI 面板
        EventBus.Publish(new PanelRequestEvent(this)); // 打开面板
    }

    /// <summary>
    /// 计算玩家当前队伍的总战力
    /// </summary>
    /// <returns>玩家队伍总战力值</returns>
    private int EvaluatePlayerTeamPower()
    {
        var partyMembers = PartyManager.Instance.PartyMembers;
        var totalPower = 0;
        foreach (var member in partyMembers)
        {
            // 玩家角色计算包含装备/增益在内的综合属性（GetTotalStats）
            totalPower += CharacterRuntimeData.EvaluatePowerFromStats(member.GetTotalStats());
        }

        return totalPower;
    }



    /// <summary>
    /// 计算敌方/NPC 队伍的总战力
    /// </summary>
    /// <returns>敌方队伍总战力值</returns>
    private int EvaluateEnemyTeamPower()
    {
        var totalPower = 0;
        foreach (var member in npcTeamMembers)
        {
            // 敌方角色通常计算基础属性（BaseStats）
            totalPower += CharacterRuntimeData.EvaluatePowerFromStats(member.BaseStats);
        }

        return totalPower;
    }



    /// <summary>
    /// 根据玩家与敌方的战力比值，评估并返回对应的挑战难度等级
    /// </summary>
    /// <param name="playerPower">玩家总战力</param>
    /// <param name="enemyPower">敌方总战力</param>
    /// <returns>1-10 的难度等级</returns>
    private int EvaluateDifficulty(int playerPower, int enemyPower)
    {
        float ratio = enemyPower / (float)playerPower;

        // 根据战力比值区间划分 1 ~ 10 个难度梯度
        if (ratio < 0.55f) return 1;    // 碾压
        if (ratio < 0.65f) return 2;
        if (ratio < 0.75f) return 3;
        if (ratio < 0.85f) return 4;
        if (ratio < 1.05f) return 5;    // 势均力敌
        if (ratio < 1.20f) return 6;
        if (ratio < 1.40f) return 7;
        if (ratio < 1.65f) return 8;
        if (ratio < 1.95f) return 9;
        return 10;                      // 极度危险
    }

}

using System.Linq;
using MFrameork.Event;
    
    // <summary>
/// 用于队伍成员数据初始化，添加，刷新等
/// </summary>
public class PartyManager : singleton<PartyManager>,IEventReceiver<GameModeChangedEvent>
    
{
    private PartyFieldController fieldController;

    [Header("初始化队伍")]
    [SerializeField] private CharacterDefinitionSO playerDefinition;
    [SerializeField] private List<CharacterRuntimeData> partyMenbers = new();

    [Header("领头人")]
    [SerializeField] private GameObject player;

    public List<CharacterRuntimeData> PartyMembers => partyMenbers;
    /// <summary>
    /// 用于标记队伍成员是否被隐藏
    /// </summary>
    private bool fieldActorsHidden;

#if UNITY_EDITOR
    // 临时快速检查入口：仅在 Unity 编辑器中生效，发布构建不会带入。
    // F6：探索模式交换 1、2 号位；战斗模式测试主动换位。
    // F7：战斗模式测试 2 号位队员被击退到 3 号位。
    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
            return;

        bool testSwap = keyboard.f6Key.wasPressedThisFrame;
        bool testKnockback = keyboard.f7Key.wasPressedThisFrame;
        if (!testSwap && !testKnockback)
            return;

        if (GameModeManager.Instance == null)
            return;

        if (GameModeManager.Instance.CurrentGameMode == GameMode.Explore)
        {
            if (testSwap)
            {
                SwapPosition(1, 2);
                Debug.Log("[换位测试] 探索模式：已请求交换 1、2 号位。");
            }
            return;
        }

        if (GameModeManager.Instance.CurrentGameMode != GameMode.Battle)
            return;

        BattleFieldManager battleField = FindAnyObjectByType<BattleFieldManager>();
        if (battleField == null)
        {
            Debug.LogWarning("[换位测试] 找不到 BattleFieldManager。");
            return;
        }

        int sourcePosition = testSwap ? 1 : 2;
        int targetPosition = testSwap ? 2 : 3;
        BattleUnit actorUnit = battleField.SpawnedAllyUnits
            .FirstOrDefault(unit => unit != null && unit.Entity != null &&
                                    unit.Entity.RuntimeData.Position == sourcePosition);
        if (actorUnit == null)
        {
            Debug.LogWarning($"[换位测试] 找不到当前处于 {sourcePosition} 号位的友军。");
            return;
        }

        bool started = testSwap
            ? battleField.TrySwapAllyPositions(actorUnit.Entity, targetPosition)
            : battleField.TryKnockbackAllyToPosition(actorUnit.Entity, targetPosition);

        string actionName = testSwap ? "主动换位" : "击退";
        Debug.Log(started
            ? $"[换位测试] 战斗模式：已启动{actionName}测试（{sourcePosition} → {targetPosition}）。此调试入口不扣行动资源。"
            : $"[换位测试] {actionName}未启动，请检查目标槽位、位置限制或是否正在移动。");
    }
#endif
    protected override void Awake()
    {
        base.Awake();
        
        fieldController = GetComponent<PartyFieldController>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe(this);
    }
    private void OnDisable()
    {
        EventBus.Unsubscribe(this);
    }

    private void Start()
    {
        InitParty();
    }

    private void InitParty()
    {
        if (partyMenbers.Count == 0)
        {
            CharacterRuntimeData player = new CharacterRuntimeData(playerDefinition);
            player.Position = 1;
            partyMenbers.Add(player); 
        }
    }
/// <summary>
/// 添加队伍成员——给予成员“位置”,并刷新跟随者数据
/// </summary>
/// <param name="characteDefinition"></param>
    private void AddMenbers(CharacterDefinitionSO characteDefinition)
    {
        CharacterRuntimeData newMember = new CharacterRuntimeData(characteDefinition);

        // 新成员放到当前队伍最后一个位置
        newMember.Position = partyMenbers.Count + 1;
        
        partyMenbers.Add(newMember);
        RefreshFieldFollowers();
    }
    /// <summary>
    /// 招募队伍成员,并切换游戏模式
    /// </summary>
    /// <param name="characteDefinition"></param>
    public void RecuitMember(CharacterDefinitionSO characteDefinition)
    {
        FadeController.Instance.SetStyle(FadeStyle.PanelFade);
        FadeController.Instance.FadeOut(() =>
        {
            AddMenbers(characteDefinition);
            GameModeManager.Instance.RequestChangeMode(GameMode.Explore);
            FadeController.Instance.FadeIn();
        });            
    }
    /// <summary>
    /// 刷新跟随者数据——用于队伍成员位置变动后更新跟随者站位(暗黑地牢更新：按照position排列)
    /// </summary>
    private void RefreshFieldFollowers(bool resetPositions = true)
    {
        // 按照成员的 Position 属性排序队列
        List<CharacterRuntimeData> sortedMembers = partyMenbers.OrderBy(m => m.Position).ToList();

        List<CharacterDefinitionSO> def = new List<CharacterDefinitionSO>(partyMenbers.Count);
        foreach (var member in sortedMembers)
        {
            def.Add(member.Definition);
        }
        fieldController.UpdateFollowers(def, resetPositions);

    }

    #region 队伍成员位置管理（暗黑地牢更新）
    /// <summary>
    /// 获取指定位置上的队伍成员。
    /// Position 使用 1~4。
    /// </summary>
    public CharacterRuntimeData GetMemberAtPosition(int position)
    {
        foreach (var member in partyMenbers)
        {
            if (member.Position == position)
                return member;
        }

        return null;
    }

    /// <summary>
    /// 交换两个队伍成员的位置。
    /// </summary>
    public void SwapPosition(int positionA, int positionB)
    {
        CharacterRuntimeData memberA = GetMemberAtPosition(positionA);
        CharacterRuntimeData memberB = GetMemberAtPosition(positionB);

        if (memberA == null || memberB == null)
            return;

        memberA.Position = positionB;
        memberB.Position = positionA;

        RefreshLeader();

        if (!fieldActorsHidden)
            RefreshFieldFollowers(false);
    }

    /// <summary>
    /// 将成员移动到目标位置。目标位置被占据时，中间成员向原位置方向补位。
    /// 例如 2 号移动到 4 号时，原 3、4 号分别移动到 2、3 号。
    /// </summary>
    public bool MoveMemberToPosition(CharacterRuntimeData member, int targetPosition)
    {
        if (member == null || !partyMenbers.Contains(member))
            return false;

        if (targetPosition < 1 || targetPosition > 4)
            return false;

        int oldPosition = member.Position;
        if (oldPosition < 1 || oldPosition > 4)
            return false;

        if (oldPosition == targetPosition)
            return true;

        if (targetPosition > oldPosition)
        {
            for (int position = oldPosition + 1; position <= targetPosition; position++)
            {
                CharacterRuntimeData shifted = GetMemberAtPosition(position);
                if (shifted != null && shifted != member)
                    shifted.Position = position - 1;
            }
        }
        else
        {
            for (int position = oldPosition - 1; position >= targetPosition; position--)
            {
                CharacterRuntimeData shifted = GetMemberAtPosition(position);
                if (shifted != null && shifted != member)
                    shifted.Position = position + 1;
            }
        }

        member.Position = targetPosition;
        RefreshLeader();

        // 战斗中探索队员已隐藏；回到探索模式时会按新位置重建队伍。
        if (!fieldActorsHidden)
            RefreshFieldFollowers(false);

        return true;
    }

    /// <summary>
    /// 将指定成员移动到目标位置。
    /// </summary>
    public bool MoveMember(CharacterRuntimeData member, int targetPosition)
    {
        if (member == null)
            return false;

        if (targetPosition < 1 || targetPosition > 4)
            return false;

        CharacterRuntimeData targetMember = GetMemberAtPosition(targetPosition);

        if (targetMember != null && targetMember != member)
        {
            int oldPosition = member.Position;

            member.Position = targetPosition;
            targetMember.Position = oldPosition;
        }
        else
        {
            member.Position = targetPosition;
        }

        RefreshLeader();
        if (!fieldActorsHidden)
            RefreshFieldFollowers(false);
        return true;
    }
    /// <summary>
    /// 刷新领头人——用于队伍成员位置变动后更新领头人站位
    /// </summary>
    private void RefreshLeader()
    {
        CharacterRuntimeData leader =
            partyMenbers.Find(member => member.Position == 1);

        if (leader == null)
        {
            Debug.LogWarning("没有找到 1 号位队员。");
            return;
        }

        // 更新 Player 的角色定义
        CharacterIdentity identity = player.GetComponent<CharacterIdentity>();
        identity.SetDefinition(leader.Definition);

        // 更新 Player 的探索动画机
        Animator animator = player.GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = leader.Definition.filedAnimator;
    }
    #endregion

    #region 事件监听

    /// <summary>
    /// 监听游戏模式切换事件的回调方法。
    /// 依据切换到的目标模式，控制地图上玩家角色和跟随者的显隐状态。
    /// </summary>
    /// <param name="evt">包含新旧游戏模式状态的事件对象。</param>
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == GameMode.Battle)
        {
            if (fieldActorsHidden)
                return;

            fieldActorsHidden = true;
            fieldController.SetPlayerActive(false);
            fieldController.ClearFollower();
            return;
        }

        if (evt.newMode == GameMode.Explore)
        {
            if (!fieldActorsHidden)
                return;

            fieldActorsHidden = false;
            fieldController.SetPlayerActive(true);
            RefreshFieldFollowers();
        }
    }

    #endregion
}

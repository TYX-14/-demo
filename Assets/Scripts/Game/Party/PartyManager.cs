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

// 临时测试：按 F6 交换 1、2 号位
private void Update()
{
    var keyboard = UnityEngine.InputSystem.Keyboard.current;

    if (keyboard != null &&
        keyboard.f6Key.wasPressedThisFrame)
    {
        SwapPosition(1, 2);
        Debug.Log("已请求交换 1、2 号位");
    }
}
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

        RefreshFieldFollowers(false);
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

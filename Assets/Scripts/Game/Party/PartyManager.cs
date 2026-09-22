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

    public List<CharacterRuntimeData> PartyMembers => partyMenbers;
    /// <summary>
    /// 用于标记队伍成员是否被隐藏
    /// </summary>
    private bool fieldActorsHidden;

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
            partyMenbers.Add(new CharacterRuntimeData(playerDefinition));
        }
    }

    private void AddMenbers(CharacterDefinitionSO characteDefinition)
    {
        partyMenbers.Add(new CharacterRuntimeData(characteDefinition));
        RefreshFieldFollowers();
    }

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
    /// 刷新跟随者数据
    /// </summary>
    private void RefreshFieldFollowers()
    {
        List<CharacterDefinitionSO> def = new List<CharacterDefinitionSO>(partyMenbers.Count);
        foreach (var member in partyMenbers)
        {
            def.Add(member.Definition);
        }
        fieldController.UpdateFollowers(def);

    }

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

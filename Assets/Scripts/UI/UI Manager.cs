using System;
using MFrameork.Event;
public class UIManager : MonoBehaviour,
    IEventReceiver<PanelRequestEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("根物体与特殊面板使用")]
    [SerializeField, Tooltip("探索模式下显示的总体UI根节点")]
    private GameObject fielUIroot;
    [SerializeField] private GameObject mainPanel;

    // 编队面板不属于 ActionBase 驱动的三级菜单，单独通过队列按钮打开。
    private PartyFormationPanelController partyFormationPanel;
   
    private readonly Dictionary<Type, PanelController> _PanelControllerDict = new();
    private readonly List<PanelController> _allPanelList = new();

    #region 周期函数
    private void Awake()
    {
        _PanelControllerDict.Clear();
        _allPanelList.Clear();

        GetPanelFromRoot(transform);
        partyFormationPanel = GetComponentInChildren<PartyFormationPanelController>(true);
    }

    /// <summary>
    /// 由主菜单中的“队列”按钮调用。只关闭功能面板，不关闭主菜单。
    /// </summary>
    public void OpenPartyFormationPanel()
    {
        if (partyFormationPanel == null)
            partyFormationPanel = GetComponentInChildren<PartyFormationPanelController>(true);

        if (partyFormationPanel == null)
        {
            Debug.LogError("UIManager 找不到 PartyFormationPanelController，请检查编队面板是否位于 UI Manager 子层级中。", this);
            return;
        }

        CloseAllPanels();
        partyFormationPanel.gameObject.SetActive(true);
    }

    private void Update()
    {
        var input = InputSystemController.Instance;
        if (input.GetMenuPressed())
        {
            HandleGlobalMenuInput();
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PanelRequestEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }
    private void OnDisable()
    {
        EventBus.Unsubscribe<PanelRequestEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }
    #endregion

    private void HandleGlobalMenuInput()
    {
        if (mainPanel.activeInHierarchy)
        {
            mainPanel.SetActive(false);
            GameModeManager.Instance.RequestChangeMode(GameMode.Explore);
            return;
        }
        GameModeManager.Instance.RequestChangeMode(GameMode.Pause);
        mainPanel.SetActive(true);
    }

    #region 事件相关
    /// <summary>
    /// 打开 ActionBase 对应的功能面板前，先关闭其他功能面板。
    /// </summary>
    public void OnEvent(PanelRequestEvent evt)
    {
        CloseAllPanels();

        var panelType = evt.actionBase.GetType();

        _PanelControllerDict.TryGetValue(panelType, out var panelController);
        panelController.gameObject.SetActive(true);
        panelController.SetupPanel(evt.actionBase);
    }

    /// <summary>
    /// 如果游戏模式切换，该事件关闭面板
    /// </summary>
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode != GameMode.InteractionMenu)
        {
             CloseAllPanels();         
        }
    }
    #endregion

    #region 面板控制相关
    /// <summary>
    /// 只登记带有具体 Action 类型的三级功能面板。
    /// 没有 panelActionType 的对象（例如 Main Panel）不属于功能面板，
    /// 不能加入关闭列表，否则切换功能时会把整个主菜单一起关闭。
    /// </summary>
    private void GetPanelFromRoot(Transform root)
    {
        var panels = root.GetComponentsInChildren<PanelController>(true);
        foreach (var panel in panels)
        {
            if (panel.panelActionType is null)
                continue;

            _allPanelList.Add(panel);
            _PanelControllerDict.Add(panel.panelActionType, panel);
        }
    }

    /// <summary>
    /// 关闭所有三级功能面板。编队面板不继承 PanelController，因此单独关闭。
    /// 主菜单不在功能面板列表中，会保持打开。
    /// </summary>
    private void CloseAllPanels()
    {
        foreach (var panel in _allPanelList)
        {
            panel.gameObject?.SetActive(false);
        }

        if (partyFormationPanel != null)
            partyFormationPanel.gameObject.SetActive(false);
    }
    #endregion
}
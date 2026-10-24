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
   

    private readonly Dictionary<Type, PanelController> _PanelControllerDict = new();
    private readonly List<PanelController> _allPanelList = new();

    
    

    #region 周期函数
    private void Awake()
    {
        _PanelControllerDict.Clear();
        _allPanelList.Clear();

        GetPanelFromRoot(transform);
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
    /// 该事件启动各个面板，并且设置面板
    /// </summary>
    /// <param name="evt"></param>
    public void OnEvent(PanelRequestEvent evt)
    {
        var panelType = evt.actionBase.GetType();

        _PanelControllerDict.TryGetValue(panelType, out var panelController);
        panelController.gameObject.SetActive(true);
        panelController.SetupPanel(evt.actionBase);
    }

    /// <summary>
    /// 如果游戏模式切换，该事件关闭面板
    /// </summary>
    /// <param name="evt"></param>
    /// <exception cref="System.NotImplementedException"></exception>
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
    /// 把所有三级菜单控制器获取并添加到字典
    /// </summary>
    /// <param name="root"></param>
    private void GetPanelFromRoot(Transform root)
    {
        var panels = root.GetComponentsInChildren<PanelController>(true);//panels将会被自动识别为数组
        foreach (var panel in panels)
        {
            _allPanelList.Add(panel);
            if (panel.panelActionType is null)
            {      
               continue;
            }
            _PanelControllerDict.Add(panel.panelActionType, panel);
        }
    }

    /// <summary>
    /// 遍历所有在字典的三级菜单，如果它属于激活状态，则关闭它
    /// </summary>
    private void TryHandCancelByActivePanel()
    {
        foreach (var panel in _allPanelList)
        {
            if (panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
                return;
            }
        }
    }
    /// <summary>
    /// 检查是否有三级菜单处于激活状态
    /// </summary>
    /// <returns></returns>
    private bool InAnyPanelActive()
    {
        foreach (var panel in _allPanelList)
        {
            if (panel.gameObject.activeSelf)
            {
                return true;
            }
        }
        return false;
    }

    private void CloseAllPanels()
    {
        foreach (var panel in _allPanelList)
        {
            panel.gameObject?.SetActive(false);
        }
    }
    #endregion
}

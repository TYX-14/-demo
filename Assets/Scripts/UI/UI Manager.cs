using System;
using MFrameork.Event;
public class UIManager : MonoBehaviour,
    IEventReceiver<PanelRequestEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("根物体与特殊面板使用")]
    [SerializeField, Tooltip("探索模式下显示的总体UI根节点")]
    private GameObject fielUIroot;
    [SerializeField, Tooltip("战斗模式下显示的战斗 UI 根节点")]
    private GameObject battleUIRoot;
    [SerializeField] private GameObject mainPanel;
   

    private readonly Dictionary<Type, PanelController> _PanelControllerDict = new();
    private readonly List<PanelController> _allPanelList = new();

    // 战斗时保留主菜单所在的 Canvas，只隐藏其他探索界面节点。
    private readonly Dictionary<GameObject, bool> _fieldUIOriginalStates = new();
    private bool _fieldUIFilteredForBattle;

    
    

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
        if (mainPanel == null)
        {
            Debug.LogWarning("UIManager：Main Panel 尚未绑定，请在 Inspector 中指定主菜单根物体。");
            return;
        }

        var modeManager = GameModeManager.Instance;
        if (modeManager == null)
        {
            Debug.LogWarning("UIManager：找不到 GameModeManager，无法切换暂停状态。");
            return;
        }

        if (mainPanel.activeSelf)
        {
            mainPanel.SetActive(false);

            if (modeManager.CurrentGameMode == GameMode.Pause)
            {
                modeManager.RequestChangeMode(modeManager.ModeBeforePause);
            }

            return;
        }

        if (modeManager.CurrentGameMode != GameMode.Pause)
        {
            modeManager.RequestChangeMode(GameMode.Pause);
        }

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

        SwitchFieldAndBattleUI(evt.newMode);
    }
    #endregion

    /// <summary>
    /// 根据游戏模式切换探索界面与战斗界面。
    /// 战斗模式显示 Battle UI，其余模式显示 Field UI。
    /// </summary>
    private void SwitchFieldAndBattleUI(GameMode mode)
    {
        // 暂停只是覆盖在当前模式上的状态，不应切换或关闭当前模式的 Canvas。
        if (mode == GameMode.Pause)
        {
            return;
        }

        bool isBattleMode = mode == GameMode.Battle;

        if (isBattleMode)
        {
            if (fielUIroot != null)
            {
                if (mainPanel != null && mainPanel.transform.IsChildOf(fielUIroot.transform))
                {
                    HideFieldUIExceptMainPanel();
                }
                else
                {
                    fielUIroot.SetActive(false);
                }
            }

            if (battleUIRoot != null)
            {
                battleUIRoot.SetActive(true);
            }

            return;
        }

        RestoreFieldUI();

        if (fielUIroot != null)
        {
            fielUIroot.SetActive(true);
        }

        if (battleUIRoot != null)
        {
            battleUIRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 保留主菜单及其父级路径，让主菜单在战斗时仍处于有效 Canvas 中。
    /// </summary>
    private void HideFieldUIExceptMainPanel()
    {
        if (_fieldUIFilteredForBattle || fielUIroot == null || mainPanel == null)
        {
            return;
        }

        _fieldUIOriginalStates.Clear();

        foreach (Transform item in fielUIroot.GetComponentsInChildren<Transform>(true))
        {
            if (item.gameObject != fielUIroot)
            {
                _fieldUIOriginalStates[item.gameObject] = item.gameObject.activeSelf;
            }
        }

        HideUnrelatedFieldUIBranches(fielUIroot.transform, mainPanel.transform);
        _fieldUIFilteredForBattle = true;
    }

    private void HideUnrelatedFieldUIBranches(Transform current, Transform protectedPanel)
    {
        foreach (Transform child in current)
        {
            if (child == protectedPanel)
            {
                // 保留主菜单本身及其所有子物体的原始状态。
                continue;
            }

            if (protectedPanel.IsChildOf(child))
            {
                // 主菜单的祖先必须保持激活，才能继续使用 Field UI 的 Canvas。
                child.gameObject.SetActive(true);
                HideUnrelatedFieldUIBranches(child, protectedPanel);
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void RestoreFieldUI()
    {
        if (!_fieldUIFilteredForBattle)
        {
            return;
        }

        foreach (var pair in _fieldUIOriginalStates)
        {
            if (pair.Key != null)
            {
                pair.Key.SetActive(pair.Value);
            }
        }

        _fieldUIOriginalStates.Clear();
        _fieldUIFilteredForBattle = false;
    }

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

using UnityEngine.UI;
using UnityEngine.Pool;
using Unity.VisualScripting;
using MFrameork.Event;
using UnityEngine.EventSystems;
public class InteractionUIcontroller : MonoBehaviour,
    IEventReceiver<InteractionChangeEvent>,
    IEventReceiver<InteractionMequestEvent>,
    IEventReceiver<GameModeChangedEvent>

{
    

    [Header("Head Icon")]
    [SerializeField] private RectTransform actionIconHolder;
    [SerializeField] private GameObject actionIconPrefab;

    [Header("二级菜单按钮")]
    [SerializeField] private RectTransform actionMuenHolder;
    [SerializeField] private GameObject actionMeunBottonPrefab;

    private ObjectPool<GameObject> _iconPool;
    private ObjectPool<GameObject> _meunButtonPool;
    private readonly List<GameObject> _actionIcons = new List<GameObject>(8);//已激活的Icon列表
    private readonly List<GameObject> _actionButton = new List<GameObject>(8);//已激活的button列表

    private IReadOnlyList<ActionCommandInfo> _currentCommandList;
    private Transform _headAnchor;

    private InteractionBase _target;

    #region 代码周期
    private void Awake()
    {
        InitPool();
        actionIconHolder.gameObject.SetActive(false);
        actionMuenHolder.gameObject.SetActive(false);
        
    }

    private void Update()
    {
        if (GameModeManager.Instance.CurrentGameMode == GameMode.Battle)
        {
            return;
        }
        var input = InputSystemController.Instance;
        if (input.GetUIancelPressed())
        {
            CloseMenu(true);
            GameModeManager.Instance.RequestChangeMode(GameMode.Explore);
        }
    }
    private void LateUpdate()
    {
        if (!actionIconHolder.gameObject.activeSelf || _headAnchor == null)
        {
            return;
        }
        UpdateHeadIconPosition();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<InteractionChangeEvent>(this);
        EventBus.Subscribe<InteractionMequestEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<InteractionChangeEvent>(this);
        EventBus.Unsubscribe<InteractionMequestEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    #endregion
    private void InitPool()
    {
        _iconPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(actionIconPrefab, actionIconHolder),//Instantiate实例化函数。内存中复制一份资源，并在场景中创建一个独立的游戏对象
                                                                              //参数一：克隆对象  二：要克隆到哪
            actionOnGet: (GameObject obj) =>
            {
                obj.SetActive(true);
                obj.transform.SetAsLastSibling();//将物体移动到父物体下的最后一个位置，使其在视觉上显示在最顶层（不被遮挡）。
            },
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            defaultCapacity: 8,//子起步时预留的位置（初始容量）。
            maxSize:20
            );

        _meunButtonPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(actionMeunBottonPrefab, actionMuenHolder),

            actionOnGet: (GameObject obj) =>
            {
                obj.SetActive(true);
                obj.transform.SetAsLastSibling();
            },
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            defaultCapacity: 8,
            maxSize: 20
            );
    }

    private void UpdateHeadIconPosition()
    {
        if (_target == null || !_target.isActiveAndEnabled)
        {
            HideHeadIcons();
            return;

        }
        var worldPos = _headAnchor.position;
        var screenPos = Camera.main.WorldToScreenPoint(worldPos);
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform,screenPos,null,out var localPoint))
        {
            actionIconHolder.anchoredPosition = localPoint;
        }
    }

    #region 对象池相关方法
    private void SyncPool(List<GameObject>activeList,ObjectPool<GameObject>pool,int tragetCount)//根据目标数量自动调整对象池和缓存临时列表
    {
        while (activeList.Count > tragetCount)
        {
            int lastIndex = activeList.Count - 1;
            GameObject item = activeList[lastIndex];
            pool.Release(item);
            activeList.RemoveAt(lastIndex);
        }

        while (activeList.Count < tragetCount)
        {
            GameObject item = pool.Get();
            activeList.Add(item);
        }
    }

    private void ReleseAll(List<GameObject> activeList, ObjectPool<GameObject> pool)//归还所有对象
    {
        for (int i = 0; i < activeList.Count; i++)
        {
            pool.Release(activeList[i]);
        }
        activeList.Clear();
    }


    #endregion

    #region 事件相关方法
    /// <summary>
    /// 启动一级菜单
    /// </summary>
    /// <param name="evt"></param>
    public void OnEvent(InteractionChangeEvent evt)//实现可交互状态改变事件
    {
        _target = evt.target;
        if (!evt.inRange || evt.target == null)
        {
            //关闭一级菜单
            HideHeadIcons();
            return;
        }
        _currentCommandList = evt.target.CacheCommadInfo;
        _headAnchor = evt.target.HeadAchor;
        
        ShowHeadIcons();

        
    }
    /// <summary>
    /// 启动二级菜单
    /// </summary>
    /// <param name="evt"></param>
    /// <exception cref="System.NotImplementedException"></exception>
    public void OnEvent(InteractionMequestEvent evt)//实现请求菜单事件
    {
        //关闭一级菜单
        HideHeadIcons();
        if (!evt.target.gameObject.activeSelf)
        {
            return;
        }

        actionMuenHolder.gameObject.SetActive(true);
        OpenMenu(evt.target);
        


    }
    /// <summary>
    /// 如果切换游戏模式为探索，则重新显示一级菜单
    /// </summary>
    /// <param name="evt"></param>
    public void OnEvent(GameModeChangedEvent evt)
    {
       if (evt.newMode == GameMode.InteractionMenu)
        {
            return;
        }
       if (evt.newMode == GameMode.Explore)
        {
            if (_currentCommandList is not null && _currentCommandList.Count > 0)
            {
                ShowHeadIcons();
            }
        }

    }
    #endregion
    /// <summary>
    /// 显示一级菜单
    /// </summary>
    private void ShowHeadIcons()
    {
        if (_currentCommandList == null||_currentCommandList.Count == 0)//如果npc命令列表没有可执行命令，直接return
        {
            return;
        }
        actionIconHolder.gameObject.SetActive(true);

        SyncPool(_actionIcons, _iconPool, _currentCommandList.Count);

        for (int i = 0; i < _actionIcons.Count; i++)
        {
            var obj = _actionIcons[i];
            var cmd = _currentCommandList[i];

            obj.GetComponent<Image>().sprite = cmd.Icon;
        }
        
    }

    private void OpenMenu(InteractionBase target)
    {
        //通知GameMode切换到UI
        GameModeManager.Instance.RequestChangeMode(GameMode.InteractionMenu);

        Button fistBtn = null;
        SyncPool(_actionButton,_meunButtonPool,_currentCommandList.Count);
        for (int i = 0; i < _actionButton.Count; i++)
        {
            var btn = _actionButton[i];
            var cmd = _currentCommandList[i];

            int commandIndex=i;
            btn.GetComponent<ActionMeunButton>().SetButton(cmd, () =>
            {
                target.ExecuteCommandFromUI(commandIndex);
                CloseMenu(false);
            });

            if (fistBtn is null)
            {
                fistBtn = btn.GetComponent<Button>();
            }
        }

        if (fistBtn is not null)
        {
            fistBtn.Select();
            EventSystem.current.SetSelectedGameObject(fistBtn.gameObject);
        }

    }

    /// <summary>
    /// 判断关闭菜单逻辑——如果参数为Fasle，则关闭一级菜单（固定关闭二级）
    /// </summary>
    private void CloseMenu(bool restoreHeadIcons)
    {

        HideActionMenu();
       if (restoreHeadIcons)
        {
            ShowHeadIcons();
        }
       else
        {
            HideHeadIcons();
        }

    }
    /// <summary>
    /// 隐藏一级菜单
    /// </summary>
    private void HideHeadIcons()
    {
        actionIconHolder?.gameObject.SetActive(false);
        ReleseAll(_actionIcons, _iconPool);

    }
    /// <summary>
    /// 隐藏二级菜单
    /// </summary>
    private void HideActionMenu()
    {
        actionMuenHolder?.gameObject.SetActive(false);
        ReleseAll(_actionButton, _meunButtonPool);
    }

   
}

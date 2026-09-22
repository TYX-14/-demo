

using System;

public class InteractionBase : MonoBehaviour//该脚本挂在可交互NPC上
{
    [Header("Sign信息")]
    public Transform HeadAchor;//Sign图标

    private AllyDefinitionSO _currentInteractor;//当前试图交互的玩家人物
    private ActionBase[] _actionsCache; //缓存该npc身上的交互事件列表
    private readonly List<ActionCommandInfo> _cacheCommadInfo = new List<ActionCommandInfo>(8);
    private readonly List<VisibleActionEntry> _visibleActionEntries = new List<VisibleActionEntry>(8);        
    public IReadOnlyList<ActionCommandInfo> CacheCommadInfo => _cacheCommadInfo;
    


    public struct VisibleActionEntry
    {
        public ActionBase Action;
        public ActionCommandInfo CommandInfo;
    }

    private void Awake()
    {
        
        CacheAction();
    }
    public void Interact(AllyDefinitionSO interactor)
    {
        EventBus.Publish(new InteractionMequestEvent(this));
    }
    public void OnFocus(AllyDefinitionSO interactor)
    {
        CacheAction();
        _currentInteractor = interactor;
        RebuildCommands();
        PublishEvent(true);

        
    }
    public void OnLoseFocus()
    {
        _currentInteractor=null;
        _cacheCommadInfo.Clear();
        PublishEvent(false);
        //玩家离开范围内，显示Sign
        HeadAchor.gameObject.SetActive(true);
    }

    private void CacheAction()=> _actionsCache = GetComponents<ActionBase>();//拿到npc身上的所有命令，做成数组

    private void RebuildCommands()//用于给命令排序
    {
        _cacheCommadInfo.Clear();
        _visibleActionEntries.Clear();
        for (int i = 0; i < _actionsCache.Length; i++)
        {
            
            var action = _actionsCache[i];
            if (!CanAnyPartyMemberExecute(action))//TODO:之后需要改成小队职业检测
            {
                continue;
            }
            _visibleActionEntries.Add(new VisibleActionEntry 
            {
                Action = action,
                CommandInfo =action.CommandInfo 
            } );
        }
        if (_visibleActionEntries.Count > 1)
        {
            _visibleActionEntries.Sort((a,b)=>a.CommandInfo.Order.CompareTo(b.CommandInfo.Order));//将列表中元素按照Order排序
        }
        
        for (int i = 0; i < _visibleActionEntries.Count; i++)//将排序后的命令用来初始化列表
        {
            
            _cacheCommadInfo.Add(_visibleActionEntries[i].CommandInfo);
            
            
        }
        //如果当前npc身上命令大于1，则关闭Sign
        if (_visibleActionEntries.Count > 0)
        {
            HeadAchor.gameObject.SetActive(false);
        }
        
    }

    /// <summary>
    /// 从队伍中判断有没有人可以执行互动
    /// </summary>
    /// <returns></returns>
    private bool CanAnyPartyMemberExecute(ActionBase action)
    {
        var PartyMember = PartyManager.Instance.PartyMembers;
        if (PartyMember.Count > 0)
        {
            for (int i = 0; i < PartyMember.Count; i++)
            {
                
                if (PartyMember[i].Definition == null)
                {
                    continue;
                }
                if (action.CanShow(PartyMember[i].Definition as AllyDefinitionSO))
                {
                    
                    return true;
                }
                
            }
            
        }
        return false;
    }


    private void  PublishEvent(bool inRange)
    {  
       
        EventBus.Publish(new InteractionChangeEvent(this, inRange));
       
        
    }
    #region UI回调入口
    /// <summary>
    /// 尝试根据索引和交互者条件,调用UI。
    /// </summary>
    /// <param name="commandIndex">命令在可见列表中的索引。</param>
    /// <returns>
    /// 如果索引有效且命令成功执行，返回 true；
    /// 如果索引越界或交互者条件不满足，返回 false。
    /// </returns>
    public bool ExecuteCommandFromUI(int commandIndex)
    {
        //判断是否出界
        if (commandIndex >= _visibleActionEntries.Count || commandIndex < 0)
        {
            return false;
        }

        var action = _visibleActionEntries[commandIndex].Action;
        // 检查当前交互者是否满足执行条件
        if (!action.CanExecute(_currentInteractor))
        {
            return false;
        }
        action.TriggerAction(_currentInteractor);
        return true;
    }
    #endregion



}

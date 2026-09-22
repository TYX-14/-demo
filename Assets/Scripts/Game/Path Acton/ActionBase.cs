
public abstract class ActionBase : MonoBehaviour
{
       public PlayerJob MacthJob = PlayerJob.Any;

    public ActionCommandInfo CommandInfo;

    /// <summary>
    /// 是否展示可执行的一级菜单
    /// </summary>
    /// <param name="interactor"></param>
    /// <returns></returns>
    public virtual bool CanShow(AllyDefinitionSO interactor)
    {
        //基类方法：展示可以执行的命令
        return IsJobMatch(interactor);
    }

    public virtual bool CanExecute(AllyDefinitionSO interactor)
    {
        return true;
    }

    public virtual void TriggerAction(AllyDefinitionSO interactor)
    {
        //如果有需要其他逻辑再重写
        //如：偷窃，需执行打开背包界面
        Execute(interactor);//默认执行玩家命令
    }

    public virtual void Execute(object contextData = null)
    {
        //基类方法：执行玩家指令
    }
    
    protected virtual bool IsJobMatch(AllyDefinitionSO interactor)
    {
        if (MacthJob == PlayerJob.Any)
        {
            return true;
        }
        return interactor.job == MacthJob;

    }
}

[System.Serializable]
public struct ActionCommandInfo
{
    public string displayName;

    public Sprite Icon;
    public int Order;
}

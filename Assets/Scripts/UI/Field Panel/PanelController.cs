using System;
using Unity.VisualScripting;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class PanelController : MonoBehaviour
{
    [Header("Action")]
    public ActionBase CurrentAction;

    [Header("Focus Navigation")]
    public Button FirstButton;

   
    

    [Header("命令图标")]
    [SerializeField] private Sprite actionIcon;

    /// <summary>
    /// 为了防止非面板控制器的UI，需要子类重写这个属性,让其等于当前的行动指令
    /// </summary>
    public virtual Type panelActionType => null;

    


    public virtual void SetupPanel(ActionBase actionBase)
    {
        CurrentAction = actionBase;
        actionIcon = actionBase.CommandInfo.Icon;

        
        
    }
    /// <summary>
    /// 取消当前的3级面板
    /// </summary>
    public virtual void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    /// <summary>
    /// 关闭所有UI窗口，切换至探索模式
    /// </summary>
    protected void OnCancel()
    {
        GameModeManager.Instance.RequestChangeMode(GameMode.Explore);
        ClosePanel();
        
    }

    public virtual void OnConfirm()
    {
        CurrentAction.Execute();

    }
    /// <summary>
    /// 判断面板的关闭是否需要额外的控制
    /// </summary>
    /// <returns></returns>
    public virtual bool HandleCancelInput()
    {
        return false;
    }

    /// <summary>
    /// 这个方法用于将面板的FirstButton设置为选中状态
    /// </summary>
    protected void SetDefaultSelection()
    {
        FirstButton.Select();
        EventSystem.current.SetSelectedGameObject(FirstButton.gameObject);
    }
    protected void SetItemButtonSelection(ItemButton itemButton)
    {
        itemButton.CurrentButton.Select();
        EventSystem.current.SetSelectedGameObject(itemButton.CurrentButton.gameObject);
    }
    /// <summary>
    /// 用于取消绑定按钮过后重新绑定
    /// </summary>
    /// <param name="button">目标按钮</param>
    /// <param name="unityAction">该按钮身上绑定的函数方法</param>
    protected void RebindButtons(Button button,UnityAction unityAction)// （绑定按钮）
    {
        if (button is null) return;
        button.onClick.RemoveAllListeners(); // 1. 移除监听器
        button.onClick.AddListener(unityAction);// 2.添加监听器
    }

}

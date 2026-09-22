
using System;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 三级菜单控制器
/// </summary>
public class InquiriePanelController : PanelController
{
    [Header("打听面板")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private Image npcAvatar;
    [SerializeField] private TMP_Text messageTitleText;
    [SerializeField] private TMP_Text messageContentText;

    private InquireAction _currentAction;
    private int _currentIndex = -1;

    [Header("按钮")]
    [SerializeField] private Button confirmButton;


    public override Type panelActionType => typeof(InquireAction);

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);

        FirstButton = confirmButton;//把确认按钮到赋值到FirsButton中

        BindButtons();
        SetDefaultSelection();

        _currentAction = (InquireAction)actionBase;

        ApplyMessge(_currentAction.PickRandomMessageIndex());
    }

    private void ApplyMessge(int messgeIndex)
    {
        _currentAction.GetInquireActionData(messgeIndex, out InquireActionData inquireActionData);
        _currentIndex = messgeIndex;

        npcNameText.text = inquireActionData.personName;
        npcAvatar.sprite = inquireActionData.SpriteOverrdie;
        messageContentText.text = inquireActionData.message;
        messageTitleText.text = inquireActionData.title;
        
    }

    /// <summary>
    /// 移除之前的监听器，添加onclick关于关闭所有UI面板的监听
    /// </summary>
    private void BindButtons()// （绑定按钮）
    {
        confirmButton.onClick.RemoveAllListeners(); // 1. 移除监听器
        confirmButton.onClick.AddListener(OnCancel);// 2.添加监听器
    }

}

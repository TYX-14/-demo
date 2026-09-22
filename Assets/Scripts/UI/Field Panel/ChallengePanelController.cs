using UnityEngine.UI;
using TMPro;
using System;
public class ChallengePanelController : PanelController
{
    // 在 Unity 检查器（Inspector）中创建一个名为 "Challenge Panel" 的分组标题，方便按模块管理变量
    [Header("Challenge Panel")]

    // 序列化私有字段，使其可以在 Unity 编辑器中拖拽赋值
    // 用于显示 NPC 名字的 TextMeshPro 文本组件
    [SerializeField] private TMP_Text npcNameText;

    // 用于显示挑战难度的 TextMeshPro 文本组件
    [SerializeField] private TMP_Text difficultyText;

    // 用于显示角色立绘或头像的 UI 图片组件
    [SerializeField] private Image characterImage;


    // 用于确认操作的按钮组件（例如：接受挑战）
    [SerializeField] private Button confirmButton;

    // 用于取消操作的按钮组件（例如：拒绝/关闭面板）
    [SerializeField] private Button cancelButton;

    // 重写属性（通常用于面板管理器，来标识当前面板的类型）
    // 提示：原图中的 typeof(challen) 未写完，这里自动纠正补全为对应的类名或枚举类型（请根据你的实际项目类名修改，例如 ChallengePanel）
    public override Type panelActionType => typeof(ChallengeAction);

    /// <summary>
    /// 初始化并配置挑战面板的 UI 显示与按钮事件
    /// </summary>
    /// <param name="actionBase">传入的基础动作数据</param>
    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);

        // 类型转换并获取当前交互的角色数据
        ChallengeAction challengeAction = (ChallengeAction)actionBase;
        var currentCharacter = challengeAction.CurrentCharacter;

        // 刷新 UI 文本与图片
        npcNameText.text = currentCharacter.Name;
        difficultyText.text = "难度：" + challengeAction.LastDifficulty.ToString();
        characterImage.sprite = currentCharacter.Portrait;

        // 绑定按钮事件
        RebindButtons(confirmButton,OnConfirm);
        RebindButtons(cancelButton, OnCancel);

        // 设置默认选中的 UI 按钮（通常用于手柄/键盘导航）
        FirstButton = confirmButton;
        SetDefaultSelection();
    }
}

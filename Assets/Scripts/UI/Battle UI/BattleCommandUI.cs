using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗指令 UI。
/// 负责指令面板的显示、隐藏，以及将按钮点击映射为统一的战斗指令类型。
/// 具体战斗行为由后续的战斗逻辑负责处理。
/// </summary>
public class BattleCommandUI : singleton<BattleCommandUI>
{
    [Header("战斗指令面板")]
    [SerializeField, Tooltip("战斗指令面板的 CanvasGroup")]
    private CanvasGroup commandPanelCanvasGroup;

    [SerializeField, Tooltip("独立于布局组的战斗指令面板背景图")]
    private Image commandPanelBackgroundImage;

    [Header("战斗指令按钮")]
    [SerializeField, Tooltip("攻击按钮")]
    private Button attackButton;

    [SerializeField, Tooltip("交换位置按钮")]
    private Button swapPositionButton;

    [SerializeField, Tooltip("道具按钮")]
    private Button itemButton;

    [SerializeField, Tooltip("逃跑按钮")]
    private Button escapeButton;

    protected override void Awake()
    {
        base.Awake();

        // 如果场景中存在重复实例，基类会销毁重复对象。
        if (Instance != this)
        {
            return;
        }

        BindCommandButtons();
        ClosePanel();
    }

    /// <summary>
    /// 绑定按钮与战斗指令类型。
    /// </summary>
    private void BindCommandButtons()
    {
        if (attackButton != null)
        {
            attackButton.onClick.AddListener(
                () => OnCommandButtonClicked(BattleCommandType.Attack));
        }

        if (swapPositionButton != null)
        {
            swapPositionButton.onClick.AddListener(
                () => OnCommandButtonClicked(BattleCommandType.SwapPosition));
        }

        if (itemButton != null)
        {
            itemButton.onClick.AddListener(
                () => OnCommandButtonClicked(BattleCommandType.Item));
        }

        if (escapeButton != null)
        {
            escapeButton.onClick.AddListener(
                () => OnCommandButtonClicked(BattleCommandType.Escape));
        }
    }

    /// <summary>
    /// 显示战斗指令面板。
    /// </summary>
    public void ShowPanel()
    {
        SetBackgroundVisible(true);

        if (commandPanelCanvasGroup == null)
        {
            Debug.LogWarning("BattleCommandUI：尚未绑定战斗指令面板的 CanvasGroup。", this);
            return;
        }

        commandPanelCanvasGroup.alpha = 1f;
        commandPanelCanvasGroup.interactable = true;
        commandPanelCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// 隐藏战斗指令面板并禁用交互。
    /// </summary>
    public void ClosePanel()
    {
        SetBackgroundVisible(false);

        if (commandPanelCanvasGroup == null)
        {
            return;
        }

        commandPanelCanvasGroup.alpha = 0f;
        commandPanelCanvasGroup.interactable = false;
        commandPanelCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 控制独立背景图的显隐。背景图已移出 CanvasGroup/布局控制范围，因此需要单独同步状态。
    /// </summary>
    private void SetBackgroundVisible(bool isVisible)
    {
        if (commandPanelBackgroundImage != null)
        {
            commandPanelBackgroundImage.enabled = isVisible;
        }
    }

    /// <summary>
    /// 统一的战斗指令入口。目前只接收指令类型，具体行为后续接入战斗系统。
    /// </summary>
    private void OnCommandButtonClicked(BattleCommandType commandType)
    {
        Debug.Log($"选择了战斗指令：{commandType}", this);
    }
}

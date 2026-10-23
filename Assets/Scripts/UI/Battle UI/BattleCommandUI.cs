using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗指令 UI。
/// 负责控制指令面板与独立背景图的淡入淡出，以及将按钮点击映射为统一的战斗指令类型。
/// 具体战斗行为由后续的战斗逻辑负责处理。
/// </summary>
public class BattleCommandUI : singleton<BattleCommandUI>
{
    [Header("战斗指令面板")]
    [SerializeField, Tooltip("战斗指令面板的 CanvasGroup")]
    private CanvasGroup commandPanelCanvasGroup;

    [SerializeField, Tooltip("独立于布局组的战斗指令面板背景图")]
    private Image commandPanelBackgroundImage;

    [SerializeField, Min(0.01f), Tooltip("指令面板淡入淡出的持续时间（秒）")]
    private float fadeDuration = 0.2f;

    [Header("战斗指令按钮")]
    [SerializeField, Tooltip("攻击按钮")]
    private Button attackButton;

    [SerializeField, Tooltip("点击攻击后打开的二级技能面板控制器")]
    private BattleSkillPanelController skillPanelController;

    [SerializeField, Tooltip("交换位置按钮")]
    private Button swapPositionButton;

    [SerializeField, Tooltip("道具按钮")]
    private Button itemButton;

    [SerializeField, Tooltip("逃跑按钮")]
    private Button escapeButton;

    private Coroutine fadeCoroutine;
    private float backgroundOriginalAlpha = 1f;

    protected override void Awake()
    {
        base.Awake();

        // 如果场景中存在重复实例，基类会销毁重复对象。
        if (Instance != this)
        {
            return;
        }

        if (commandPanelBackgroundImage != null)
        {
            backgroundOriginalAlpha = commandPanelBackgroundImage.color.a;
        }

        BindCommandButtons();
        SetPanelImmediately(false);
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
    /// 淡入显示战斗指令面板。
    /// </summary>
    public void ShowPanel()
    {
        if (commandPanelCanvasGroup != null)
        {
            commandPanelCanvasGroup.interactable = true;
            commandPanelCanvasGroup.blocksRaycasts = true;
        }

        if (commandPanelBackgroundImage != null)
        {
            commandPanelBackgroundImage.enabled = true;
        }

        StartFade(true);
    }

    /// <summary>
    /// 淡出隐藏战斗指令面板，并立即禁用按钮交互。
    /// </summary>
    public void ClosePanel()
    {
        if (commandPanelCanvasGroup != null)
        {
            commandPanelCanvasGroup.interactable = false;
            commandPanelCanvasGroup.blocksRaycasts = false;
        }

        StartFade(false);
    }

    private void StartFade(bool show)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (!isActiveAndEnabled)
        {
            SetPanelImmediately(show);
            return;
        }

        fadeCoroutine = StartCoroutine(FadePanel(show));
    }

    private IEnumerator FadePanel(bool show)
    {
        float startGroupAlpha = commandPanelCanvasGroup != null
            ? commandPanelCanvasGroup.alpha
            : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        float startBackgroundAlpha = commandPanelBackgroundImage != null
            ? commandPanelBackgroundImage.color.a
            : 0f;
        float targetBackgroundAlpha = show ? backgroundOriginalAlpha : 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // 使用平滑缓动，避免线性渐变显得生硬。
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            if (commandPanelCanvasGroup != null)
            {
                commandPanelCanvasGroup.alpha =
                    Mathf.Lerp(startGroupAlpha, targetAlpha, easedProgress);
            }

            SetBackgroundAlpha(Mathf.Lerp(
                startBackgroundAlpha, targetBackgroundAlpha, easedProgress));

            yield return null;
        }

        if (commandPanelCanvasGroup != null)
        {
            commandPanelCanvasGroup.alpha = targetAlpha;
        }

        SetBackgroundAlpha(targetBackgroundAlpha);

        if (!show && commandPanelBackgroundImage != null)
        {
            commandPanelBackgroundImage.enabled = false;
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// 立即设置初始状态，避免场景初始化时背景图先闪现再淡出。
    /// </summary>
    private void SetPanelImmediately(bool show)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (commandPanelCanvasGroup != null)
        {
            commandPanelCanvasGroup.alpha = show ? 1f : 0f;
            commandPanelCanvasGroup.interactable = show;
            commandPanelCanvasGroup.blocksRaycasts = show;
        }

        if (commandPanelBackgroundImage != null)
        {
            commandPanelBackgroundImage.enabled = show;
            SetBackgroundAlpha(show ? backgroundOriginalAlpha : 0f);
        }
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (commandPanelBackgroundImage == null)
        {
            return;
        }

        Color color = commandPanelBackgroundImage.color;
        color.a = alpha;
        commandPanelBackgroundImage.color = color;
    }

    /// <summary>
    /// 统一的战斗指令入口。目前只接收指令类型，具体行为后续接入战斗系统。
    /// </summary>
    private void OnCommandButtonClicked(BattleCommandType commandType)
    {
        if (commandType == BattleCommandType.Attack)
        {
            if (skillPanelController == null)
            {
                Debug.LogWarning("尚未绑定 BattleSkillPanelController，请在 Inspector 中设置技能二级面板控制器。", this);
                return;
            }

            // 技能面板是攻击按钮的子物体，只切换二级面板显隐。
            // 不关闭一级指令面板，避免父级 CanvasGroup 同时淡出。
            skillPanelController.TogglePanel();
            return;
        }

        // 其他指令都与技能面板互斥，先关闭技能面板，再执行对应指令逻辑。
        if (skillPanelController != null)
        {
            skillPanelController.HidePanel();
        }

        Debug.Log($"选择了战斗指令：{commandType}", this);
    }
}

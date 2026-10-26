using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 战斗 HUD 控制器。
/// 负责控制战斗中的血条面板与 CTB UI 根节点的显示。
/// </summary>
public class BattleHUDcontroller : MonoBehaviour, IEventReceiver<BattleStartedEvent>
{
    [Header("战斗 HUD 根节点")]
    [SerializeField, Tooltip("血条面板的根物体")]
    private GameObject healthBarPanelRoot;

    [SerializeField, Tooltip("CTB UI 的根物体")]
    private GameObject ctbRoot;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
    }

    /// <summary>
    /// 接收战斗开始通知。
    /// </summary>
    public void OnEvent(BattleStartedEvent evt)
    {
        SetupHUD();
    }

    /// <summary>
    /// 显示战斗 HUD。后续可在这里集中初始化战斗 HUD 的其他内容。
    /// </summary>
    private void SetupHUD()
    {
        if (healthBarPanelRoot != null)
        {
            healthBarPanelRoot.SetActive(true);
        }

        if (ctbRoot != null)
        {
            ctbRoot.SetActive(true);
        }

    }
}

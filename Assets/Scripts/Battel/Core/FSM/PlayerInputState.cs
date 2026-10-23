using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家输入状态的基础占位实现。
/// 进入状态时显示战斗指令面板，并等待指令处理逻辑将 BattleController 切换到其他状态。
/// 后续在此接入目标选择、指令确认与行动结算流程。
/// </summary>
public class PlayerInputState : BattleState
{
    public PlayerInputState(BattleController controller) : base(controller)
    {
    }

    public override IEnumerator Enter()
    {
        if (BattleCommandUI.Instance != null)
            BattleCommandUI.Instance.ShowPanel();

        yield break;
    }

    public override IEnumerator Execute()
    {
        // 等待玩家指令系统调用 BattleController.SetState(...)。
        while (_controller.IsBattleRunning && _controller.CurrentState == this)
            yield return null;
    }

    public override IEnumerator Exit()
    {
        if (BattleCommandUI.Instance != null)
            BattleCommandUI.Instance.ClosePanel();

        yield break;
    }
}

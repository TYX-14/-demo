using System.Collections;
using UnityEngine;

/// <summary>
/// 敌方 AI 状态的基础占位实现。
/// 当前尚未实现敌方决策与行动结算，因此暂时等待后续 AI 逻辑切换状态。
/// 后续在此接入技能选择、目标选择、行动执行与行动结束后的状态切换。
/// </summary>
public class EnemyAIState : BattleState
{
    public EnemyAIState(BattleController controller) : base(controller)
    {
    }

    public override IEnumerator Execute()
    {
        Debug.LogWarning("EnemyAIState：敌方 AI 尚未实现，当前暂时等待后续逻辑切换状态。");

        while (_controller.IsBattleRunning && _controller.CurrentState == this)
            yield return null;
    }
}

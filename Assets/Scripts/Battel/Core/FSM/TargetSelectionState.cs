using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 等待玩家选择行动目标。
/// Esc 或鼠标右键会取消选择并返回玩家输入状态，不消耗行动。
/// </summary>
public class TargetSelectionState : BattleState
{
    private readonly BattleActionContext _actionContext;
    private Camera _battleCamera;

    public TargetSelectionState(BattleController controller, BattleActionContext actionContext)
        : base(controller)
    {
        _actionContext = actionContext;
    }

    public override IEnumerator Enter()
    {
        _battleCamera = Camera.main;

        if (_actionContext == null || _actionContext.Skill == null)
        {
            Debug.LogWarning("TargetSelectionState：待选择行动或技能为空。");
            _controller.CancelPendingAction();
            _controller.SetState(new PlayerInputState(_controller));
            yield break;
        }

        SetValidTargetCursors(true);
        yield break;
    }

    public override IEnumerator Execute()
    {
        while (_controller.IsBattleRunning && _controller.CurrentState == this)
        {
            if (WasCancelPressed())
            {
                _controller.CancelPendingAction();
                _controller.SetState(new PlayerInputState(_controller));
                yield break;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // 点击 UI 只操作 UI，不应同时被当作战场目标输入。
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    yield return null;
                    continue;
                }

                if (_battleCamera == null)
                    _battleCamera = Camera.main;

                if (_battleCamera != null)
                {
                    BattleEntity clickedTarget = FindClickedTarget(mouse.position.ReadValue());
                    if (clickedTarget != null &&
                        _actionContext.TrySetTarget(
                            clickedTarget,
                            _controller.AllEntities,
                            _controller.GetLogicalPosition))
                    {
                        _controller.SetState(new PerformActionState(_controller, _actionContext));
                        yield break;
                    }
                }
            }

            yield return null;
        }
    }

    public override IEnumerator Exit()
    {
        SetValidTargetCursors(false);
        yield break;
    }

    private bool WasCancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
               (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    private BattleEntity FindClickedTarget(Vector2 screenPosition)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || !entity.IsAlive || entity.IsPlayer || entity.Unit == null)
                continue;

            if (!_actionContext.Skill.TargetPositions.Contains(_controller.GetLogicalPosition(entity)))
                continue;

            if (entity.Unit.ContainsScreenPoint(screenPosition, _battleCamera))
                return entity;
        }

        return null;
    }

    private void SetValidTargetCursors(bool visible)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || entity.Unit == null)
                continue;

            bool isValidTarget = entity.IsAlive &&
                                 !entity.IsPlayer &&
                                 _actionContext != null &&
                                 _actionContext.Skill != null &&
                                 _actionContext.Skill.TargetPositions.Contains(
                                     _controller.GetLogicalPosition(entity));

            entity.Unit.SetTargetSelection(visible && isValidTarget);
        }
    }
}

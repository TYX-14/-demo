using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 等待玩家选择行动目标。
/// 鼠标悬停在合法敌人身上时，只显示该敌人身上的目标提示图标；
/// Esc 或鼠标右键会取消选择并返回玩家输入状态，不消耗行动。
/// </summary>
public class TargetSelectionState : BattleState
{
    private readonly BattleActionContext _actionContext;
    private Camera _battleCamera;
    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

    public TargetSelectionState(BattleController controller, BattleActionContext actionContext)
        : base(controller)
    {
        _actionContext = actionContext;
    }

    public override IEnumerator Enter()
    {
        _battleCamera = Camera.main;
        Debug.Log("【目标选择】进入 TargetSelectionState。");
        if (_battleCamera == null)
            Debug.LogWarning("【目标选择】未找到 MainCamera，无法进行鼠标目标检测。");

        if (_actionContext == null || _actionContext.Skill == null)
        {
            Debug.LogWarning("TargetSelectionState：待选择行动或技能为空。");
            _controller.CancelPendingAction();
            _controller.SetState(new PlayerInputState(_controller));
            yield break;
        }

        // 目标选择开始时，显示所有符合当前技能站位要求的敌人脚下光圈。
        // 目标小手仍只在鼠标悬停到具体合法目标时显示。
        SetAllTargetCursors(false);
        SetLegalTargetHighlights(true);
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
            if (mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                UpdateHoveredTargetCursor(mouse, mousePosition);

                if (mouse.leftButton.wasPressedThisFrame)
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
                        BattleEntity clickedTarget = FindTargetAtScreenPosition(mousePosition);
                        if (clickedTarget != null &&
                            _actionContext.TrySetTarget(
                                clickedTarget,
                                _controller.AllEntities,
                                _controller.GetLogicalPosition))
                        {
                            // 只有玩家确认目标后才关闭技能二级面板。
                            // 取消分支不关闭它，因此返回输入状态时可以继续选择技能。
                            if (BattleCommandUI.Instance != null)
                                BattleCommandUI.Instance.HideSkillPanel();

                            _controller.SetState(new PerformActionState(_controller, _actionContext));
                            yield break;
                        }
                    }
                }
            }
            else
            {
                SetAllTargetCursors(false);
            }

            yield return null;
        }
    }

    public override IEnumerator Exit()
    {
        // 无论确认、取消还是战斗中断，都不能让目标提示残留。
        SetAllTargetCursors(false);
        SetLegalTargetHighlights(false);
        yield break;
    }

    private bool WasCancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
               (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    private void UpdateHoveredTargetCursor(Mouse mouse, Vector2 screenPosition)
    {
        bool pointerOverUI = IsPointerOverUI(screenPosition);
        if (pointerOverUI)
        {
            SetAllTargetCursors(false);
            return;
        }

        if (_battleCamera == null)
            _battleCamera = Camera.main;

        BattleEntity hoveredTarget = _battleCamera != null
            ? FindTargetAtScreenPosition(screenPosition)
            : null;


        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || entity.Unit == null)
                continue;

            entity.Unit.SetTargetSelection(entity == hoveredTarget);
        }
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointerData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _uiRaycastResults);

        GameObject uiHit = null;
        foreach (RaycastResult result in _uiRaycastResults)
        {
            if (result.module is GraphicRaycaster)
            {
                uiHit = result.gameObject;
                break;
            }
        }

        return uiHit != null;
    }



    private BattleEntity FindTargetAtScreenPosition(Vector2 screenPosition)
    {
        if (_battleCamera == null)
            return null;

        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (!IsLegalTarget(entity))
                continue;

            if (entity.Unit.ContainsScreenPoint(screenPosition, _battleCamera))
                return entity;
        }

        return null;
    }

    /// <summary>
    /// 统一判断当前技能能否选择该实体。光圈与鼠标命中共用此规则。
    /// </summary>
    private bool IsLegalTarget(BattleEntity entity)
    {
        if (entity == null || !entity.IsAlive || entity.IsPlayer || entity.Unit == null ||
            _actionContext == null || _actionContext.Skill == null)
            return false;

        return _actionContext.Skill.TargetPositions.Contains(
            _controller.GetLogicalPosition(entity));
    }

    private void SetLegalTargetHighlights(bool visible)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity?.Unit == null)
                continue;

            entity.Unit.SetTargetHighlight(visible && IsLegalTarget(entity));
        }
    }

    private void SetAllTargetCursors(bool visible)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity?.Unit != null)
                entity.Unit.SetTargetSelection(visible);
        }
    }
}

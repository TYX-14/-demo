using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 等待玩家选择行动目标。
/// 目标合法性与实际范围解析统一交给 BattleTargeting；本状态只处理输入、光圈和鼠标提示。
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

        if (_actionContext == null || _actionContext.Skill == null ||
            !BattleTargeting.RequiresManualSelection(_actionContext.Skill))
        {
            Debug.LogWarning("TargetSelectionState：待选择行动或技能无效，或该技能不需要手动选择目标。");
            _controller.CancelPendingAction();
            _controller.SetState(new PlayerInputState(_controller));
            yield break;
        }

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
                UpdateHoveredTargetCursor(mousePosition);

                if (mouse.leftButton.wasPressedThisFrame)
                {
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
                            BattleTargeting.TryResolveSelectedTarget(
                                _actionContext.Skill,
                                _actionContext.Actor,
                                clickedTarget,
                                _controller.AllEntities,
                                _controller.GetLogicalPosition,
                                out int selectedPosition,
                                out List<BattleEntity> resolvedTargets))
                        {
                            _actionContext.SetResolvedTargets(clickedTarget, selectedPosition, resolvedTargets);

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
        ClearTargetFeedback();
        yield break;
    }

    /// <summary>立即清除本状态负责的目标光圈和鼠标小手提示。</summary>
    public void ClearTargetFeedback()
    {
        SetAllTargetCursors(false);
        SetLegalTargetHighlights(false);
    }

    private bool WasCancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
               (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    private void UpdateHoveredTargetCursor(Vector2 screenPosition)
    {
        if (IsPointerOverUI(screenPosition))
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

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _uiRaycastResults);

        foreach (RaycastResult result in _uiRaycastResults)
        {
            if (result.module is GraphicRaycaster)
                return true;
        }

        return false;
    }

    private BattleEntity FindTargetAtScreenPosition(Vector2 screenPosition)
    {
        if (_battleCamera == null)
            return null;

        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || entity.Unit == null || !IsLegalTarget(entity))
                continue;

            if (entity.Unit.ContainsScreenPoint(screenPosition, _battleCamera))
                return entity;
        }

        return null;
    }

    private bool IsLegalTarget(BattleEntity entity)
    {
        return entity != null && entity.Unit != null &&
               BattleTargeting.IsLegalTarget(
                   _actionContext?.Skill,
                   _actionContext?.Actor,
                   entity,
                   _controller.GetLogicalPosition);
    }

    private void SetLegalTargetHighlights(bool visible)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity?.Unit != null)
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

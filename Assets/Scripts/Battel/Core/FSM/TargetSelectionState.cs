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

        // 玩家真正选出技能后才显示蓝色行动光圈；所有合法目标用半透明小手标识。
        ShowActorRing();
        RefreshTargetCursors(null);
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
                RefreshTargetCursors(mousePosition);

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
                RefreshTargetCursors(null);
            }

            yield return null;
        }
    }

    public override IEnumerator Exit()
    {
        ClearTargetFeedback();
        yield break;
    }

    /// <summary>收起本次技能选择的全部反馈：小手与玩家行动光圈。</summary>
    public void ClearTargetFeedback()
    {
        SetAllTargetCursors(false);
        HideActorRing();
    }

    private bool WasCancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
               (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    /// <summary>
    /// 刷新所有单位的小手状态：
    /// 合法目标常驻半透明小手；鼠标悬停时，单体技能只高亮指针下的目标，
    /// AoE 技能会同时高亮所有实际会被该技能命中的目标（即全部合法目标）。
    /// </summary>
    private void RefreshTargetCursors(Vector2? screenPosition)
    {
        BattleEntity hoveredTarget = null;

        if (screenPosition.HasValue && !IsPointerOverUI(screenPosition.Value))
        {
            if (_battleCamera == null)
                _battleCamera = Camera.main;

            if (_battleCamera != null)
                hoveredTarget = FindTargetAtScreenPosition(screenPosition.Value);
        }

        // AoE 的结算范围与合法目标列表一致，因此悬停到任意一个合法目标时，
        // 其余合法目标也应一起进入高亮浮动，提前展示“这一发会打到谁”。
        bool highlightAllAreaTargets = hoveredTarget != null &&
                                       _actionContext?.Skill != null &&
                                       _actionContext.Skill.TargetType == SkillTargetType.AoE;

        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity == null || entity.Unit == null)
                continue;

            BattleUnit.TargetCursorMode mode;
            if (!IsLegalTarget(entity))
                mode = BattleUnit.TargetCursorMode.Hidden;
            else if (entity == hoveredTarget || highlightAllAreaTargets)
                mode = BattleUnit.TargetCursorMode.Hovered;
            else
                mode = BattleUnit.TargetCursorMode.Candidate;

            entity.Unit.SetTargetCursorMode(mode);
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

    /// <summary>玩家选定技能后点亮自己的蓝色行动光圈。</summary>
    private void ShowActorRing()
    {
        BattleVFXManager vfxManager = BattleVFXManager.Instance;
        if (vfxManager != null)
            vfxManager.ShowCurrentActorRing(_actionContext?.Actor);
    }

    /// <summary>取消或结束技能选择时收起行动光圈。</summary>
    private static void HideActorRing()
    {
        BattleVFXManager vfxManager = BattleVFXManager.Instance;
        if (vfxManager != null)
            vfxManager.HideCurrentActorRing();
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

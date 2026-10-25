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
    private BattleEntity _lastLoggedHoveredTarget;
    private bool _hasLoggedHoverResult;
    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    private string _lastLoggedUIHit;

    public TargetSelectionState(BattleController controller, BattleActionContext actionContext)
        : base(controller)
    {
        _actionContext = actionContext;
    }

    public override IEnumerator Enter()
    {
        _battleCamera = Camera.main;
        _lastLoggedHoveredTarget = null;
        _hasLoggedHoverResult = false;
        _lastLoggedUIHit = null;

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

        // 进入目标选择时先隐藏所有提示，等待鼠标实际悬停到某个合法目标。
        SetAllTargetCursors(false);
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
        // 无论确认、取消还是战斗中断，都不能让提示图标残留在敌人身上。
        SetAllTargetCursors(false);
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
        // UI 与战斗实体使用不同的命中机制：先检查鼠标下是否有真正参与射线检测的 UI。
        // UI 命中时不把鼠标输入同时解释成战场悬停；若意外命中全屏透明 UI，日志会指出对象名。
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

        // 只在悬停目标发生变化时记录日志，避免每帧刷屏。
        if (!_hasLoggedHoverResult || hoveredTarget != _lastLoggedHoveredTarget)
        {
            _hasLoggedHoverResult = true;
            _lastLoggedHoveredTarget = hoveredTarget;
            if (hoveredTarget == null)
                Debug.Log($"【目标选择】鼠标位置 {screenPosition} 未命中合法敌人。");
            else
                Debug.Log($"【目标选择】鼠标命中合法敌人：{hoveredTarget.Definition.name}，逻辑站位：{_controller.GetLogicalPosition(hoveredTarget)}。");
        }

        // 每帧只允许一个合法目标显示提示；鼠标离开目标或移到无效目标时立即隐藏。
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
        {
            LogUIHitIfChanged(null);
            return false;
        }

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

        LogUIHitIfChanged(uiHit);
        return uiHit != null;
    }

    private void LogUIHitIfChanged(GameObject uiHit)
    {
        string hitName = uiHit != null ? uiHit.name : null;
        if (hitName == _lastLoggedUIHit)
            return;

        _lastLoggedUIHit = hitName;
        if (hitName == null)
            Debug.Log("【目标选择】鼠标下没有 UI 射线命中，允许检测战斗实体。");
        else
            Debug.Log($"【目标选择】鼠标被 UI 阻挡：{hitName}。若它是透明/全屏背景，请检查其 Graphic 的 Raycast Target。 ");
    }

    private BattleEntity FindTargetAtScreenPosition(Vector2 screenPosition)
    {
        if (_actionContext == null || _actionContext.Skill == null || _battleCamera == null)
            return null;

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

    private void SetAllTargetCursors(bool visible)
    {
        foreach (BattleEntity entity in _controller.AllEntities)
        {
            if (entity?.Unit != null)
                entity.Unit.SetTargetSelection(visible);
        }
    }
}

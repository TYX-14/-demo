using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特写期间共用的镜头与界面处理：
/// 对准参战者、隐藏常驻 HUD（CTB / 血条 / 指令面板），结束时原样恢复。
///
/// 攻击特写、美德 / 折磨特写等任何演出都调用这两个方法，
/// 保证镜头语言和界面行为一致，不必各自重复一遍。
///
/// 无状态：每次进入都重新查找目标对象，不跨战斗缓存引用。
/// </summary>
public static class BattleCloseup
{
    /// <summary>当前是否处于攻击特写中。设计约定：特写期间不出现队友气泡。</summary>
    public static bool IsActive { get; private set; }

    /// <summary>进入特写：收起光圈与常驻 HUD，镜头对准参战者并开始推进。</summary>
    public static IEnumerator Enter(BattleEntity actor, IReadOnlyList<BattleEntity> targets)
    {
        IsActive = true;
        BattleActionCamera camera = Object.FindAnyObjectByType<BattleActionCamera>();
        BattleHUDcontroller hud = Object.FindAnyObjectByType<BattleHUDcontroller>();

        if (BattleVFXManager.Instance != null)
            BattleVFXManager.Instance.HideActionHighlights();

        if (hud != null)
            hud.SetCloseupActive(true);

        if (camera != null)
        {
            yield return camera.FocusOnParticipants(actor, targets);
            camera.BeginFocusPush();
        }
    }

    /// <summary>退出特写：停止推进、镜头复位、恢复常驻 HUD 与脚下血条。</summary>
    public static IEnumerator Exit()
    {
        BattleActionCamera camera = Object.FindAnyObjectByType<BattleActionCamera>();
        BattleHUDcontroller hud = Object.FindAnyObjectByType<BattleHUDcontroller>();

        if (camera != null && camera.isActiveAndEnabled)
        {
            camera.EndFocusPush();
            yield return camera.RestoreBattleView();
        }

        if (hud != null)
            hud.SetCloseupActive(false);

        IsActive = false;
    }
}

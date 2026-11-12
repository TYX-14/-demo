using System;
using UnityEngine;

/// <summary>特写滑动相对攻击方向的关系。</summary>
public enum SkillSlideMode
{
    None = 0,
    /// <summary>顺攻击方向（攻击者 → 目标）。</summary>
    AlongAttack = 1,
    /// <summary>逆攻击方向。</summary>
    AgainstAttack = 2
}

/// <summary>
/// 技能的攻击特写滑动配置。
/// 只影响演出期间角色在舞台上的临时位移，不改变逻辑站位，
/// 也不参与击退、拉前这类真实位移的结算。
/// </summary>
[Serializable]
public class SkillSlideConfig
{
    [Tooltip("攻击者在特写中的滑动方向。")]
    public SkillSlideMode attacker = SkillSlideMode.None;

    [Tooltip("被攻击者在特写中的滑动方向。")]
    public SkillSlideMode targets = SkillSlideMode.None;

    [Min(0f), Tooltip("滑动距离（世界单位）。角色宽度约 1，建议 0.05~0.25。")]
    public float distance = 0.1f;

    [Min(0.01f), Tooltip("滑出所需时间。这段时间会计入命中后的演出时长，建议 0.3~0.8。")]
    public float outDuration = 0.6f;

    public bool HasAnySlide =>
        attacker != SkillSlideMode.None || targets != SkillSlideMode.None;
}

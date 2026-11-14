using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个事件下的台词组：选中事件，然后在下面填这个事件会说的话。
/// </summary>
[Serializable]
public class BarkEventLines
{
    [Tooltip("这个组对应的事件。")]
    public BattleMomentKind Event = BattleMomentKind.EnemyKilled;

    [TextArea(1, 3), Tooltip("该事件会说的话，随机挑一句。")]
    public string[] Lines;

    [Range(0f, 1f), Tooltip("事件发生时真正开口的概率。")]
    public float Chance = 1f;

    [Tooltip("说完后对全队造成的压力变化。正数增压，负数减压，0 表示没有影响。")]
    public int TeamStressDelta;
}

/// <summary>
/// 一个角色的完整台词库。
/// 三种精神状态各有一份列表，每个列表里按事件分组配置台词。
/// </summary>
[CreateAssetMenu(fileName = "AllyBark", menuName = "Battle/Ally Bark")]
public class AllyBarkSO : ScriptableObject
{
    [Header("普通状态")]
    [Tooltip("普通状态下，各事件分别说什么。")]
    public List<BarkEventLines> normalLines = new();

    [Header("美德状态")]
    [Tooltip("美德状态下，各事件分别说什么。")]
    public List<BarkEventLines> virtueLines = new();

    [Header("折磨状态")]
    [Tooltip("折磨状态下，各事件分别说什么。")]
    public List<BarkEventLines> afflictionLines = new();

    /// <summary>取某种精神状态下的台词组。</summary>
    public List<BarkEventLines> GetLines(CharacterSpiritState state)
    {
        switch (state)
        {
            case CharacterSpiritState.Virtue:
                return virtueLines;
            case CharacterSpiritState.Affliction:
                return afflictionLines;
            default:
                return normalLines;
        }
    }
}

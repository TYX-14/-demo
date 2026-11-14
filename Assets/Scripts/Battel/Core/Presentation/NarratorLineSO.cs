using UnityEngine;

/// <summary>
/// 一类旁白台词的配置。
/// 同一 Kind 可以有多份资产，旁白会按权重随机，并按各自的概率和冷却决定是否开口。
/// </summary>
[CreateAssetMenu(fileName = "NarratorLine", menuName = "Battle/Narrator Line")]
public class NarratorLineSO : ScriptableObject
{
    [Tooltip("这条台词对应的瞬间类型。")]
    public BattleMomentKind Kind = BattleMomentKind.CriticalHit;

    [TextArea(1, 3), Tooltip("候选台词。每次随机挑一条，且尽量避开上一次用过的那条。")]
    public string[] Texts;

    [Range(0f, 1f), Tooltip("触发时真正开口的概率。暗黑地牢式旁白通常不用每次都说话。")]
    public float Chance = 0.4f;

    [Min(0f), Tooltip("同类型最短间隔（秒），避免连续战斗里反复播同一类。")]
    public float Cooldown = 15f;

    [Min(1), Tooltip("多条同类候选之间的权重，数值越大越容易被选中。")]
    public int Weight = 1;
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 队友说话调度器。
///
/// 台词不再集中配置，而是挂在每个角色的定义上（普通 / 美德 / 折磨三份列表），
/// 每条台词绑定一个触发事件（BattleMomentKind）。
///
/// 触发方式完全是事件驱动：
/// - 本次行动解析出一个 moment，只有存在对应事件的台词才会说话；
/// - 优先让事件的相关者说话（行动者 / 承受者），否则在拥有该台词的队友中随机；
/// - 每回合最多一位角色说话；
/// - 说完对全队造成一次压力变化，走和压力结算相同的通道。
/// </summary>
public class BattleBarkDirector : MonoBehaviour
{
    [Header("气泡时序")]
    [SerializeField, Min(0f)] private float bubbleFadeIn = 0.08f;

    [Header("文字颜色")]
    [SerializeField, Tooltip("普通状态下的台词颜色。")]
    private Color normalTextColor = Color.white;

    [SerializeField, Tooltip("美德状态下的台词颜色。")]
    private Color virtueTextColor = new(1f, 0.78f, 0.22f, 1f);

    [SerializeField, Tooltip("折磨状态下的台词颜色。")]
    private Color afflictionTextColor = new(1f, 0.29f, 0.25f, 1f);

    [SerializeField, Min(0f), Tooltip("气泡停留多久后开始应用压力影响并淡出。")]
    private float bubbleHold = 1.2f;

    [SerializeField, Min(0f)] private float bubbleFadeOut = 0.25f;

    [Header("说话压力结算")]
    [SerializeField, Min(0f), Tooltip("气泡出现后，第一颗压力飘字延迟多久。")]
    private float barkStressDelay = 0.05f;

    [SerializeField, Min(0f), Tooltip("相邻位置压力飘字的间隔。")]
    private float barkStressStagger = 0.15f;

    [SerializeField, Min(0f), Tooltip("最后一颗压力飘字出现后的收尾等待。")]
    private float barkStressTail = 0.2f;

    private int lastSpeakRound = int.MinValue;
    private readonly List<BattleEntity> speakers = new();

    [Header("调试")]
    [SerializeField, Tooltip("开启后在 Console 输出跳过原因，方便排查台词为什么不触发。")]
    private bool logDiagnostics = true;

    private readonly struct BarkCandidate
    {
        public readonly BattleEntity Entity;
        public readonly BarkEventLines Line;
        public readonly string Text;

        public BarkCandidate(BattleEntity entity, BarkEventLines line, string text)
        {
            Entity = entity;
            Line = line;
            Text = text;
        }
    }

    /// <summary>按本次行动解析出的全部事件（优先级从高到低），尝试让一位队友说话。</summary>
    public IEnumerator TryPlayBark(BattleController controller, IReadOnlyList<BattleMoment> moments)
    {
        if (controller == null)
            yield break;

        // 设计约定：攻击特写期间不出现队友气泡。
        if (BattleCloseup.IsActive)
            yield break;

        if (moments == null || moments.Count == 0)
            yield break;

        int round = controller.CurrentRoundNumber;

        // 同一回合只允许一位角色说话。
        if (round == lastSpeakRound)
            yield break;

        CollectSpeakers(controller);
        if (speakers.Count == 0)
            yield break;

        // 按优先级依次尝试：高优先级的没人配台词时，降级到下一个事件。
        BarkCandidate candidate = default;
        bool found = false;

        for (int i = 0; i < moments.Count && !found; i++)
            found = TryFindCandidate(moments[i], out candidate);

        if (!found)
        {
            if (logDiagnostics)
            {
                Debug.Log($"[队友说话] 本回合事件（{moments.Count} 个）：没有队友配置对应台词，跳过。");
            }

            yield break;
        }

        lastSpeakRound = round;
        yield return PlayBark(controller, candidate);
    }

    /// <summary>
    /// 在所有存活队友里找一位“当前精神状态下有对应事件台词”的人。
    /// 事件相关者（行动者 / 承受者）优先，其余随机。
    /// </summary>
    private bool TryFindCandidate(BattleMoment moment, out BarkCandidate candidate)
    {
        candidate = default;

        List<BarkCandidate> candidates = new();

        for (int i = 0; i < speakers.Count; i++)
        {
            if (TryBuildCandidate(speakers[i], moment.Kind, out BarkCandidate built))
                candidates.Add(built);
        }

        if (candidates.Count == 0)
            return false;

        // 优先让事件的直接相关者说话，更能对上情境。
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Entity == moment.Actor || candidates[i].Entity == moment.Target)
            {
                candidate = candidates[i];
                return true;
            }
        }

        candidate = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    /// <summary>按角色当前精神状态，从它的定义里找一条匹配该事件的台词。</summary>
    private static bool TryBuildCandidate(BattleEntity speaker, BattleMomentKind kind, out BarkCandidate candidate)
    {
        candidate = default;

        CharacterRuntimeData data = speaker?.RuntimeData;
        if (data == null || data.Definition == null || !speaker.IsAlive)
            return false;

        AllyBarkSO barks = data.Definition.Barks;
        if (barks == null)
            return false;

        List<BarkEventLines> entries = barks.GetLines(data.SpiritState);
        if (entries == null || entries.Count == 0)
            return false;

        int totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            BarkEventLines entry = entries[i];
            if (entry == null || entry.Event != kind || entry.Lines == null || entry.Lines.Length == 0)
                continue;

            totalWeight++;
        }

        if (totalWeight <= 0)
            return false;

        int roll = Random.Range(0, totalWeight);
        BarkEventLines picked = null;

        for (int i = 0; i < entries.Count; i++)
        {
            BarkEventLines entry = entries[i];
            if (entry == null || entry.Event != kind || entry.Lines == null || entry.Lines.Length == 0)
                continue;

            roll--;
            if (roll < 0)
            {
                picked = entry;
                break;
            }
        }

        if (picked == null)
            return false;

        if (picked.Chance < 1f && Random.value > picked.Chance)
            return false;

        string text = picked.Lines[Random.Range(0, picked.Lines.Length)];
        if (string.IsNullOrWhiteSpace(text))
            return false;

        candidate = new BarkCandidate(speaker, picked, text);
        return true;
    }

    /// <summary>精神状态决定台词颜色：普通白色、美德金色、折磨红色。</summary>
    private Color GetTextColor(CharacterSpiritState state)
    {
        switch (state)
        {
            case CharacterSpiritState.Virtue:
                return virtueTextColor;
            case CharacterSpiritState.Affliction:
                return afflictionTextColor;
            default:
                return normalTextColor;
        }
    }

    private IEnumerator PlayBark(BattleController controller, BarkCandidate candidate)
    {
        BattleUnit unit = candidate.Entity.Unit;

        // 气泡是单位预制体里的常驻节点，位置由预制体决定。
        BattleBarkBubble bubble = unit != null ? unit.BarkBubble : null;

        if (bubble == null)
        {
            if (logDiagnostics)
            {
                Debug.LogWarning(
                    $"[队友说话] {candidate.Entity.Definition?.Name} 的 BattleUnit 上没有 BattleBarkBubble 组件，台词无法显示。", this);
            }

            yield break;
        }

        bubble.Show(candidate.Text, GetTextColor(candidate.Entity.RuntimeData.SpiritState));

        yield return FadeBubble(bubble, 0f, 1f, bubbleFadeIn);

        if (bubbleHold > 0f)
            yield return new WaitForSecondsRealtime(bubbleHold);

        // 说话的情绪影响全队：走和行动结算完全相同的构造与写入路径。
        if (candidate.Line.TeamStressDelta != 0)
        {
            List<BattleStressChange> teamStress =
                BattleStressResolver.FromBark(controller, candidate.Line.TeamStressDelta);

            yield return controller.PlayStressSettlement(
                teamStress, barkStressDelay, barkStressStagger, barkStressTail);
        }

        yield return FadeBubble(bubble, 1f, 0f, bubbleFadeOut);

        bubble.Hide();
    }

    private void CollectSpeakers(BattleController controller)
    {
        speakers.Clear();

        IReadOnlyList<BattleEntity> all = controller.AllEntities;
        if (all == null)
            return;

        for (int i = 0; i < all.Count; i++)
        {
            BattleEntity entity = all[i];
            if (entity == null || !entity.IsPlayer || !entity.IsAlive || entity.RuntimeData == null)
                continue;

            speakers.Add(entity);
        }
    }

    private static IEnumerator FadeBubble(
        BattleBarkBubble bubble,
        float from,
        float to,
        float duration)
    {
        if (bubble == null)
            yield break;

        if (duration <= 0f)
        {
            bubble.SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bubble.SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        bubble.SetAlpha(to);
    }
}

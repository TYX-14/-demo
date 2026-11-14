using System.Collections;
using System.Collections.Generic;
using MFrameork.Event;
using TMPro;
using UnityEngine;

/// <summary>
/// 战斗旁白：订阅行动事实汇总，归纳成一个瞬间，按冷却/概率/权重选出台词，
/// 显示在底部字幕条上。
///
/// 特写期间照常播放——事件本身就是命中帧发出的，所以旁白会正好接在刀落下的那一刻。
/// </summary>
public class BattleNarrator : MonoBehaviour,
    IEventReceiver<BattleActionResolvedEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("台词")]
    [SerializeField, Tooltip("所有旁白台词资产。按 Kind 分组，可多份同类资产。")]
    private NarratorLineSO[] lines;

    [Header("显示")]
    [SerializeField, Tooltip("底部字幕文本。")]
    private TMP_Text lineText;
    [SerializeField, Tooltip("字幕整体透明度控制。留空时自动取同物体上的 CanvasGroup，没有则创建。")]
    private CanvasGroup canvasGroup;

    [Header("时序")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float holdDuration = 2.2f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.4f;

    [SerializeField, Min(0f), Tooltip("同一句台词的最短重复间隔（秒）。按文本内容记录，跨资产、跨战斗生效。")]
    private float sameLineCooldown = 120f;

    private readonly Dictionary<BattleMomentKind, float> lastPlayedAt = new();
    private readonly Dictionary<BattleMomentKind, int> lastTextIndex = new();
    private readonly Dictionary<string, float> lastPlayedByText = new();
    private Coroutine routine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleActionResolvedEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleActionResolvedEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);

        HideImmediately();
    }

    public void OnEvent(BattleActionResolvedEvent evt)
    {
        if (!BattleMomentResolver.TryResolve(evt.Facts, out BattleMoment moment))
            return;

        if (!TryPickLine(moment.Kind, out string line))
            return;

        Show(line);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode != GameMode.Battle)
            HideImmediately();
    }

    /// <summary>
    /// 选一条台词，依次经过四道关卡：
    /// ① 同类冷却（按 Kind，Assets 各自的 Cooldown）
    /// ② 同句冷却（按文本内容，sameLineCooldown）
    /// ③ 按资产权重抽取
    /// ④ 按该资产的 Chance 决定是否开口
    /// 任意一道不通过就静默，不退而求其次。
    /// </summary>
    private bool TryPickLine(BattleMomentKind kind, out string line)
    {
        line = null;

        if (lines == null || lines.Length == 0)
            return false;

        float now = Time.unscaledTime;
        lastPlayedAt.TryGetValue(kind, out float playedAt);
        float sinceLastPlay = now - playedAt;

        // 权重池：同类冷却已过，且至少还有一句没被同句冷却锁住。
        int totalWeight = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            NarratorLineSO asset = lines[i];
            if (!IsAssetAvailable(asset, kind, sinceLastPlay) || !HasAvailableText(asset, now))
                continue;

            totalWeight += Mathf.Max(1, asset.Weight);
        }

        if (totalWeight <= 0)
            return false;

        int roll = Random.Range(0, totalWeight);
        NarratorLineSO picked = null;

        for (int i = 0; i < lines.Length; i++)
        {
            NarratorLineSO asset = lines[i];
            if (!IsAssetAvailable(asset, kind, sinceLastPlay) || !HasAvailableText(asset, now))
                continue;

            roll -= Mathf.Max(1, asset.Weight);
            if (roll < 0)
            {
                picked = asset;
                break;
            }
        }

        if (picked == null)
            return false;

        // 不是每次都说话，避免十遍之后开始烦人。
        if (picked.Chance < 1f && Random.value > picked.Chance)
            return false;

        int index = PickAvailableTextIndex(picked, kind, now);
        if (index < 0)
            return false;

        line = picked.Texts[index];
        if (string.IsNullOrWhiteSpace(line))
            return false;

        lastTextIndex[kind] = index;
        lastPlayedAt[kind] = now;
        lastPlayedByText[line] = now;
        return true;
    }

    private static bool IsAssetAvailable(NarratorLineSO asset, BattleMomentKind kind, float sinceLastPlay)
    {
        return asset != null &&
               asset.Kind == kind &&
               asset.Texts != null &&
               asset.Texts.Length > 0 &&
               sinceLastPlay >= asset.Cooldown;
    }

    private bool HasAvailableText(NarratorLineSO asset, float now)
    {
        for (int i = 0; i < asset.Texts.Length; i++)
        {
            if (IsTextAvailable(asset.Texts[i], now))
                return true;
        }

        return false;
    }

    /// <summary>这句台词是否已经过了同句冷却。空串视为不可用。</summary>
    private bool IsTextAvailable(string text, float now)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (sameLineCooldown <= 0f)
            return true;

        return !lastPlayedByText.TryGetValue(text, out float playedAt) ||
               now - playedAt >= sameLineCooldown;
    }

    /// <summary>
    /// 在该资产里挑一句没被同句冷却锁住的。
    /// 用蓄水池抽样保证随机，同时优先避开上一次用过的那一句。
    /// </summary>
    private int PickAvailableTextIndex(NarratorLineSO asset, BattleMomentKind kind, float now)
    {
        int count = asset.Texts.Length;
        int lastIndex = lastTextIndex.TryGetValue(kind, out int last) ? last : -1;

        int firstAvailable = -1;
        int randomAvailable = -1;
        int randomCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (!IsTextAvailable(asset.Texts[i], now))
                continue;

            if (firstAvailable < 0)
                firstAvailable = i;

            if (i == lastIndex)
                continue;

            randomCount++;
            if (Random.Range(0, randomCount) == 0)
                randomAvailable = i;
        }

        return randomAvailable >= 0 ? randomAvailable : firstAvailable;
    }

    private void Show(string line)
    {
        if (lineText != null)
            lineText.text = line;

        if (canvasGroup == null)
            return;

        // 新旁白直接顶掉旧的，不排队：连击时宁可少说，也不要积压。
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return Fade(canvasGroup.alpha, 1f, fadeInDuration);

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        yield return Fade(1f, 0f, fadeOutDuration);

        routine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void HideImmediately()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (lineText != null)
            lineText.text = string.Empty;
    }
}

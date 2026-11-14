using System.Collections;
using MFrameork.Event;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕右下角的“当前我方行动者立绘”。
///
/// 表现规则与 CTB 大头像一致：
/// - 旧头像向左滑动并淡出（时长 = 滑入时长的一半）；
/// - 新头像从右侧滑入并淡入；
/// - 敌方行动或角色没有配置立绘时，只做淡出，保持留空。
///
/// 监听 CTBDisplayedActorChangedEvent，因此和 CTB 大头像完全同步。
/// </summary>
public class BattleActionPortraitUI : MonoBehaviour,
    IEventReceiver<CTBDisplayedActorChangedEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [SerializeField, Tooltip("用于显示立绘的 Image。")]
    private Image portraitImage;

    [SerializeField, Min(0f), Tooltip("新头像滑入的时长。旧头像淡出用它的一半，与 CTB 大头像一致。")]
    private float moveDuration = 0.22f;

    [SerializeField, Min(0f), Tooltip("滑入 / 滑出的水平距离。")]
    private float slideDistance = 90f;

    private Vector2 restingPosition;
    private bool restingCached;
    private Coroutine routine;

    private void Awake()
    {
        if (portraitImage != null)
        {
            portraitImage.preserveAspect = true;
            portraitImage.type = Image.Type.Simple;
        }

        CacheRestingPosition();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);

        // 父级被隐藏打断动画后，重新出现时把位置与透明度恢复到静止状态。
        RestoreRestingState();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    public void OnEvent(CTBDisplayedActorChangedEvent evt)
    {
        Play(evt.Entity);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode != GameMode.Battle)
            ClearImmediately();
    }

    private void Play(BattleEntity entity)
    {
        if (portraitImage == null)
            return;

        CacheRestingPosition();

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(TransitionPortrait(entity));
    }

    private IEnumerator TransitionPortrait(BattleEntity entity)
    {
        RectTransform rect = portraitImage.rectTransform;
        Sprite nextSprite = GetPortrait(entity);

        // 旧头像向左滑动并淡出。
        if (portraitImage.enabled && portraitImage.sprite != null)
        {
            yield return AnimatePortrait(
                rect,
                restingPosition + Vector2.left * slideDistance,
                0f,
                moveDuration * 0.5f);
        }

        portraitImage.sprite = nextSprite;
        portraitImage.enabled = nextSprite != null;

        // 敌方行动或没有立绘：淡出后留空。
        if (nextSprite == null)
        {
            rect.anchoredPosition = restingPosition;
            SetAlpha(1f);
            routine = null;
            yield break;
        }

        // 新头像从右侧滑入。
        rect.anchoredPosition = restingPosition + Vector2.right * slideDistance;
        SetAlpha(0f);

        yield return AnimatePortrait(rect, restingPosition, 1f, moveDuration);

        routine = null;
    }

    private IEnumerator AnimatePortrait(
        RectTransform rect,
        Vector2 targetPosition,
        float targetAlpha,
        float duration)
    {
        Vector2 startPosition = rect.anchoredPosition;
        float startAlpha = portraitImage.color.a;

        if (duration <= 0f)
        {
            rect.anchoredPosition = targetPosition;
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));

            yield return null;
        }

        rect.anchoredPosition = targetPosition;
        SetAlpha(targetAlpha);
    }

    /// <summary>只有我方角色、且配置了立绘才显示；敌方返回 null。</summary>
    private static Sprite GetPortrait(BattleEntity entity)
    {
        if (entity == null || !entity.IsPlayer || entity.Definition == null)
            return null;

        return entity.Definition.BattlePortrait;
    }

    private void ClearImmediately()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (portraitImage == null)
            return;

        CacheRestingPosition();
        portraitImage.sprite = null;
        portraitImage.enabled = false;
        portraitImage.rectTransform.anchoredPosition = restingPosition;
        SetAlpha(1f);
    }

    private void CacheRestingPosition()
    {
        if (restingCached || portraitImage == null)
            return;

        restingPosition = portraitImage.rectTransform.anchoredPosition;
        restingCached = true;
    }

    private void RestoreRestingState()
    {
        if (portraitImage == null)
            return;

        CacheRestingPosition();
        portraitImage.rectTransform.anchoredPosition = restingPosition;
        SetAlpha(portraitImage.sprite != null ? 1f : 0f);
    }

    private void SetAlpha(float alpha)
    {
        if (portraitImage == null)
            return;

        Color color = portraitImage.color;
        color.a = alpha;
        portraitImage.color = color;
    }
}

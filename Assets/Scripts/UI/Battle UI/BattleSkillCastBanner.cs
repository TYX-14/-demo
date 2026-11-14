using System.Collections;
using MFrameork.Event;
using TMPro;
using UnityEngine;

/// <summary>
/// 技能释放提示条：订阅 BattleSkillCastEvent，短暂显示本次释放的技能名。
/// 只消费事件里的数据，不读取战斗内部状态，因此可以独立挂在任意 Canvas 上。
/// </summary>
public class BattleSkillCastBanner : MonoBehaviour, IEventReceiver<BattleSkillCastEvent>
{
    [Header("显示内容")]
    [SerializeField, Tooltip("显示技能名的文本。")] private TMP_Text skillNameText;

    [Header("淡入淡出")]
    [SerializeField, Tooltip("整体透明度控制器。留空时自动取同物体上的 CanvasGroup，没有则创建。")]
    private CanvasGroup canvasGroup;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float holdDuration = 1.1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

    private Coroutine _routine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 起始隐藏，避免空文本露在屏幕上。
        canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleSkillCastEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleSkillCastEvent>(this);

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    public void OnEvent(BattleSkillCastEvent evt)
    {
        if (evt.Actor == null || evt.Skill == null)
            return;

        // 连续行动时用新的一条覆盖旧的，避免两条提示叠在一起。
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ShowRoutine(evt));
    }

    private IEnumerator ShowRoutine(BattleSkillCastEvent evt)
    {
        if (skillNameText != null)
            skillNameText.text = evt.Skill.SkillName;

        yield return Fade(0f, 1f, fadeInDuration);

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        yield return Fade(1f, 0f, fadeOutDuration);

        _routine = null;
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
}

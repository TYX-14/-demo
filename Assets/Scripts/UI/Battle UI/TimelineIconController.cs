using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个 CTB 时间轴头像。
/// Layout Group 负责外层槽位；动画只移动内部 Root，避免与 Horizontal Layout Group 抢写 RectTransform。
/// </summary>
public sealed class TimelineIconController : MonoBehaviour
{
    [Header("Prefab 子节点")]
    [SerializeField, Tooltip("头像外框/阵营背景 Image；为空时自动查找 Root/Border。")]
    private Image backgroundImage;

    [SerializeField, Tooltip("角色头像 Image；为空时自动查找 Root/Mask/Image。")]
    private Image portraitImage;

    [SerializeField, Tooltip("动画用的视觉根节点；为空时自动查找 Root。")]
    private RectTransform visualRoot;

    [Header("阵营背景 Sprite（在 Timeline Icon 预制体上配置）")]
    [SerializeField] private Sprite allyBackgroundSprite;
    [SerializeField] private Sprite enemyBackgroundSprite;

    private BattleEntity _boundEntity;
    private CanvasGroup _canvasGroup;
    private RectTransform _rootRect;
    private LayoutElement _layoutElement;

    public BattleEntity BoundEntity => _boundEntity;
    public string BoundEntityId => _boundEntity != null ? _boundEntity.ID : string.Empty;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        _rootRect = transform as RectTransform;
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_layoutElement == null)
            _layoutElement = GetComponent<LayoutElement>();

        if (visualRoot == null)
        {
            Transform root = transform.Find("Root");
            if (root != null)
                visualRoot = root as RectTransform;
        }

        if (backgroundImage == null)
        {
            Transform border = transform.Find("Root/Border");
            if (border != null)
                backgroundImage = border.GetComponent<Image>();
        }

        if (portraitImage == null)
        {
            Transform portrait = transform.Find("Root/Mask/Image");
            if (portrait != null)
                portraitImage = portrait.GetComponent<Image>();
        }
    }

    /// <summary>绑定实体，并根据阵营切换预制体上配置的背景 Sprite。</summary>
    public void Bind(BattleEntity entity) => Bind(new CTBDisplayNode(entity));

    public void Bind(CTBDisplayNode node)
    {
        CacheComponents();
        BattleEntity entity = node.Entity;
        _boundEntity = entity;

        if (entity == null)
        {
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = node.BattleIdleSprite;
            portraitImage.enabled = portraitImage.sprite != null;
        }
        else
        {
            Debug.LogWarning("TimelineIconController：找不到头像 Image。请确认预制体路径为 Root/Mask/Image。", this);
        }

        if (backgroundImage != null)
        {
            Sprite sideBackground = node.IsPlayer ? allyBackgroundSprite : enemyBackgroundSprite;
            if (sideBackground != null)
                backgroundImage.sprite = sideBackground;
            else
                Debug.LogWarning(
                    $"TimelineIconController：{(node.IsPlayer ? "友方" : "敌方")}背景 Sprite 未在 Timeline Icon 预制体上配置。",
                    this);
        }
        else
        {
            Debug.LogWarning("TimelineIconController：找不到背景 Image。请确认预制体路径为 Root/Border。", this);
        }
    }

    public void PrepareForReuse(Transform parent)
    {
        CacheComponents();
        transform.SetParent(parent, false);
        if (_layoutElement != null)
            _layoutElement.ignoreLayout = false;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }
        if (visualRoot != null)
            visualRoot.anchoredPosition = Vector2.zero;
        _boundEntity = null;
    }

    public void SetIgnoredByLayout(bool ignored)
    {
        CacheComponents();
        if (_layoutElement == null)
            _layoutElement = gameObject.AddComponent<LayoutElement>();
        _layoutElement.ignoreLayout = ignored;
    }

    public void SetVisualOffset(Vector2 offset)
    {
        CacheComponents();
        if (visualRoot != null)
            visualRoot.anchoredPosition = offset;
    }

    public IEnumerator PlayEnterAnimation(float offsetX, float duration)
    {
        CacheComponents();
        if (visualRoot == null || _canvasGroup == null)
            yield break;

        visualRoot.anchoredPosition = new Vector2(offsetX, 0f);
        _canvasGroup.alpha = 0f;
        yield return AnimateVisual(Vector2.zero, 1f, duration);
    }

    public IEnumerator PlayMoveAnimation(Vector2 startingOffset, float duration)
    {
        CacheComponents();
        if (visualRoot == null)
            yield break;

        visualRoot.anchoredPosition = startingOffset;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;
        yield return AnimateVisual(Vector2.zero, 1f, duration);
    }

    public IEnumerator PlayExitAnimation(float offsetX, float duration)
    {
        CacheComponents();
        if (visualRoot == null || _canvasGroup == null)
            yield break;

        Vector2 start = visualRoot.anchoredPosition;
        yield return AnimateVisual(start + new Vector2(offsetX, 0f), 0f, duration);
    }

    private IEnumerator AnimateVisual(Vector2 targetPosition, float targetAlpha, float duration)
    {
        Vector2 startPosition = visualRoot != null ? visualRoot.anchoredPosition : Vector2.zero;
        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        if (duration <= 0f)
        {
            if (visualRoot != null)
                visualRoot.anchoredPosition = targetPosition;
            if (_canvasGroup != null)
                _canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out：轻快入位，不做夸张弹跳。
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (visualRoot != null)
                visualRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        if (visualRoot != null)
            visualRoot.anchoredPosition = targetPosition;
        if (_canvasGroup != null)
            _canvasGroup.alpha = targetAlpha;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个 CTB 时间轴头像的显示控制。
/// 通过 Bind 绑定一个 BattleEntity，头像、阵营背景与行动高亮始终来自同一个实体。
/// </summary>
public sealed class TimelineIconController : MonoBehaviour
{
    [Header("Prefab 子节点")]
    [SerializeField, Tooltip("头像外框/阵营背景 Image；为空时自动查找 Root/Border。")]
    private Image backgroundImage;

    [SerializeField, Tooltip("角色头像 Image；为空时自动查找 Root/Mask/Image。")]
    private Image portraitImage;

    [Header("阵营背景")]
    [SerializeField] private Sprite allyBackground;
    [SerializeField] private Sprite enemyBackground;

    [Header("行动高亮")]
    [SerializeField, Min(1f)] private float actingScale = 1.12f;
    [SerializeField, Range(0.1f, 1f)] private float idleAlpha = 0.72f;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector3 _defaultScale;
    private BattleEntity _boundEntity;

    public BattleEntity BoundEntity => _boundEntity;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

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

        if (_rectTransform != null && _defaultScale == Vector3.zero)
            _defaultScale = _rectTransform.localScale;
    }

    /// <summary>将此头像绑定到一个战斗实体，并刷新头像与阵营背景。</summary>
    public void Bind(BattleEntity entity, Sprite allyBackgroundSprite, Sprite enemyBackgroundSprite)
    {
        CacheComponents();
        _boundEntity = entity;

        allyBackground = allyBackgroundSprite;
        enemyBackground = enemyBackgroundSprite;

        if (entity == null)
        {
            if (portraitImage != null)
                portraitImage.sprite = null;
            if (backgroundImage != null)
                backgroundImage.sprite = null;
            SetActing(false);
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = entity.Definition != null ? entity.Definition.Portrait : null;
            portraitImage.enabled = portraitImage.sprite != null;
        }
        else
        {
            Debug.LogWarning("TimelineIconController：找不到头像 Image。请确认预制体路径为 Root/Mask/Image。", this);
        }

        Sprite sideBackground = entity.IsPlayer ? allyBackground : enemyBackground;
        if (backgroundImage != null)
        {
            if (sideBackground != null)
                backgroundImage.sprite = sideBackground;
        }
        else
        {
            Debug.LogWarning("TimelineIconController：找不到阵营背景 Image。请确认预制体路径为 Root/Border。", this);
        }
    }

    /// <summary>高亮当前行动实体；非当前行动者降低透明度。</summary>
    public void SetActing(bool isActing)
    {
        CacheComponents();

        if (_rectTransform != null)
        {
            if (_defaultScale == Vector3.zero)
                _defaultScale = _rectTransform.localScale;

            _rectTransform.localScale = isActing
                ? _defaultScale * actingScale
                : _defaultScale;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = isActing ? 1f : idleAlpha;
    }
}

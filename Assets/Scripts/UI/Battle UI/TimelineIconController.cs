using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个 CTB 时间轴头像。
/// 角色头像与阵营背景均从绑定的实体及预制体自身配置读取。
/// </summary>
public sealed class TimelineIconController : MonoBehaviour
{
    [Header("Prefab 子节点")]
    [SerializeField, Tooltip("头像外框/阵营背景 Image；为空时自动查找 Root/Border。")]
    private Image backgroundImage;

    [SerializeField, Tooltip("角色头像 Image；为空时自动查找 Root/Mask/Image。")]
    private Image portraitImage;

    [Header("阵营背景 Sprite（在 Timeline Icon 预制体上配置）")]
    [SerializeField] private Sprite allyBackgroundSprite;
    [SerializeField] private Sprite enemyBackgroundSprite;

    private BattleEntity _boundEntity;

    public BattleEntity BoundEntity => _boundEntity;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
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
}

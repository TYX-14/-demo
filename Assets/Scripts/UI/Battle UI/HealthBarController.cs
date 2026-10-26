using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个角色的战斗状态栏。
/// 只绑定一个 CharacterRuntimeData，负责显示名字、HP、SP、压力，并提供当前行动者外观接口。
/// </summary>
public class HealthBarController : MonoBehaviour
{
    [Header("自动从预制体层级查找，也可在 Inspector 手动指定")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text spText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider spSlider;
    [SerializeField] private StressMeterUI stressMeter;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform rootTransform;

    [Header("当前行动者外观")]
    [SerializeField, Tooltip("当前行动角色使用的背景图。未设置时只改变缩放，不改变图片。")]
    private Sprite actingBackgroundSprite;
    [SerializeField, Min(1f)] private float actingScale = 1.2f;

    private CharacterRuntimeData boundCharacter;
    private Sprite defaultBackgroundSprite;
    private Vector3 defaultRootScale = Vector3.one;
    private bool hasCapturedDefaults;

    private void Awake()
    {
        ResolveReferences();
        CaptureDefaults();
        Refresh();
    }

    private void OnEnable()
    {
        CharacterStressSystem.CharacterUpdated += HandleCharacterUpdated;
        Refresh();
    }

    private void OnDisable()
    {
        CharacterStressSystem.CharacterUpdated -= HandleCharacterUpdated;
    }

    /// <summary>将此状态栏绑定到唯一的角色运行时数据。</summary>
    public void Bind(CharacterRuntimeData character)
    {
        if (boundCharacter != character)
        {
            boundCharacter = character;
            if (stressMeter != null)
                stressMeter.Bind(character);
        }

        Refresh();
    }

    /// <summary>由行动顺序系统调用，设置此状态栏是否属于当前行动角色。</summary>
    public void SetActing(bool isActing)
    {
        ResolveReferences();
        CaptureDefaults();

        if (backgroundImage != null)
        {
            if (isActing && actingBackgroundSprite != null)
                backgroundImage.sprite = actingBackgroundSprite;
            else
                backgroundImage.sprite = defaultBackgroundSprite;
        }

        if (rootTransform != null)
            rootTransform.localScale = isActing ? defaultRootScale * actingScale : defaultRootScale;
    }

    public CharacterRuntimeData BoundCharacter => boundCharacter;

    private void HandleCharacterUpdated(CharacterRuntimeData character)
    {
        if (character == boundCharacter)
            Refresh();
    }

    private void Refresh()
    {
        ResolveReferences();

        if (boundCharacter == null)
        {
            if (nameText != null) nameText.text = string.Empty;
            if (hpText != null) hpText.text = "0/0";
            if (spText != null) spText.text = "0/0";
            if (hpSlider != null) { hpSlider.minValue = 0; hpSlider.maxValue = 1; hpSlider.value = 0; }
            if (spSlider != null) { spSlider.minValue = 0; spSlider.maxValue = 1; spSlider.value = 0; }
            if (stressMeter != null) stressMeter.Bind(null);
            return;
        }

        StatBlock stats = boundCharacter.GetTotalStats();
        int maxHp = Mathf.Max(0, stats.MaxHP);
        int maxSp = Mathf.Max(0, stats.MaxSP);
        int currentHp = Mathf.Clamp(boundCharacter.CurrentHP, 0, maxHp);
        int currentSp = Mathf.Clamp(boundCharacter.CurrentSP, 0, maxSp);

        if (nameText != null)
            nameText.text = boundCharacter.DisplayName;
        if (hpText != null)
            hpText.text = $"{currentHp}/{maxHp}";
        if (spText != null)
            spText.text = $"{currentSp}/{maxSp}";

        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = Mathf.Max(1, maxHp);
            hpSlider.value = currentHp;
        }

        if (spSlider != null)
        {
            spSlider.minValue = 0;
            spSlider.maxValue = Mathf.Max(1, maxSp);
            spSlider.value = currentSp;
        }

        if (stressMeter != null)
            stressMeter.Bind(boundCharacter);
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (rootTransform == null)
        {
            Transform root = transform.Find("Root");
            if (root != null)
                rootTransform = root as RectTransform;
        }

        if (nameText == null) nameText = FindText("Name");
        if (hpText == null) hpText = FindText("HPtext");
        if (spText == null) spText = FindText("SPtext");

        if (hpSlider == null)
        {
            Transform slider = transform.Find("Root/HP Slider");
            if (slider != null) hpSlider = slider.GetComponent<Slider>();
        }

        if (spSlider == null)
        {
            Transform slider = transform.Find("Root/SP Slider");
            if (slider != null) spSlider = slider.GetComponent<Slider>();
        }

        if (stressMeter == null)
            stressMeter = GetComponentInChildren<StressMeterUI>(true);
    }

    private TMP_Text FindText(string objectName)
    {
        Transform target = transform.Find($"Root/{objectName}");
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private void CaptureDefaults()
    {
        if (hasCapturedDefaults)
            return;

        if (backgroundImage == null || rootTransform == null)
            ResolveReferences();

        if (backgroundImage != null)
            defaultBackgroundSprite = backgroundImage.sprite;
        if (rootTransform != null)
            defaultRootScale = rootTransform.localScale;

        hasCapturedDefaults = true;
    }
}

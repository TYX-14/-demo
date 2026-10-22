using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 编队界面单个角色条目的视图。
/// 名字、等级、血量、头像分别绑定，方便在 Inspector 中自由布局。
/// 可用于四个固定队伍槽位，也可用于英雄名册预制体。
/// </summary>
[RequireComponent(typeof(Button))]
public class PartyFormationEntryView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("可分别拖拽绑定的 UI")]
    [SerializeField] private TMP_Text positionText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image characterImage;

    private Button entryButton;
    private CharacterRuntimeData currentMember;
    private PartyFormationPanelController panelController;
    private int configuredSlotPosition;

    public int ConfiguredSlotPosition => configuredSlotPosition;
    public CharacterRuntimeData CurrentMember => currentMember;
    public RectTransform GetCharacterImageRect() => characterImage != null ? characterImage.rectTransform : null;

    public void ConfigureInteraction(PartyFormationPanelController controller, int slotPosition = 0)
    {
        panelController = controller;
        configuredSlotPosition = slotPosition;
    }

    public Button EntryButton
    {
        get
        {
            if (entryButton == null)
                entryButton = GetComponent<Button>();

            return entryButton;
        }
    }

    private void Awake()
    {
        entryButton = GetComponent<Button>();
    }

    public void SetMember(CharacterRuntimeData member, string positionLabel = "", bool showInPartySuffix = false)
    {
        currentMember = member;

        if (member == null)
        {
            SetEmpty(positionLabel);
            return;
        }

        if (positionText != null)
            positionText.text = positionLabel;

        if (characterNameText != null)
            characterNameText.text = showInPartySuffix ? $"{member.DisplayName}（队伍中）" : member.DisplayName;

        if (levelText != null)
            levelText.text = $"Lv. {member.Level}";

        if (hpText != null)
        {
            StatBlock stats = member.GetTotalStats();
            hpText.text = $"HP {member.CurrentHP}/{stats.MaxHP}";
        }

        if (characterImage != null)
        {
            Sprite portrait = member.Definition != null ? member.Definition.LeftIdle : null;
            characterImage.sprite = portrait;
            characterImage.enabled = portrait != null;
        }
    }

    public void SetEmpty(string positionLabel = "")
    {
        currentMember = null;
        if (positionText != null)
            positionText.text = positionLabel;

        if (characterNameText != null)
            characterNameText.text = "空槽";

        if (levelText != null)
            levelText.text = string.Empty;

        if (hpText != null)
            hpText.text = string.Empty;

        if (characterImage != null)
        {
            characterImage.sprite = null;
            characterImage.enabled = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentMember == null || panelController == null || EntryButton == null)
            return;

        panelController.BeginCharacterDrag(this, currentMember, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (panelController != null)
            panelController.UpdateCharacterDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (panelController != null)
            panelController.EndCharacterDrag(this, eventData.position);
    }


}

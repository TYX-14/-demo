using System;
using TMPro;
using UnityEngine.UI;
public class RecruitPanelController : PanelController
{
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image characterImage;

    [Header("°´Å¥")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button CancelButton;

    public override Type panelActionType => typeof(RecruitiActon);
    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);

        RecruitiActon recruitiActon = (RecruitiActon)actionBase;
        npcNameText.text = recruitiActon.CurrentCharacter.name;
        characterImage.sprite = recruitiActon.CurrentCharacter.Portrait;
        
        levelText.text = recruitiActon.CurrentCharacter.BaseLevel.ToString();

        BindButtons();
        SetDefaultSelection();
    }

    private void BindButtons()
    {
        RebindButtons(CancelButton, OnCancel);
        RebindButtons(confirmButton, OnConfirm);

    }
}

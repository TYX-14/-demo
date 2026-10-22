using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainPaneController : PanelController
{
    [Header("主菜单")]
    [SerializeField] private Button itemButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button partyButton;
    [SerializeField] private TMP_Text currencyAmountText;

    [Header("面板")]
    [SerializeField] private ItemPanelControl itemPanelControl;
    [SerializeField] private EquipmentPanelController equipmentPanelController;
    [SerializeField] private PartyFormationPanelController partyPanelController;
    [SerializeField] private CanvasGroup leftCanvas;

    private GameObject _currentPanel;
    private Button _lastButton;

    private void Awake()
    {
        _currentPanel = itemPanelControl.gameObject;

        RebindButtons(itemButton, OpenItemPanel);
        RebindButtons(equipmentButton, OpenEquipmentPanel);
        RebindButtons(partyButton, OpenPartyPanel);

        UpdateCurrencyDisplayer();
    }

    private void OnEnable()
    {
        FirstButton = itemButton;
        SetDefaultSelection();
    }

    private void OpenItemPanel()
    {
        OpenPanel(itemPanelControl, itemButton);
        itemPanelControl.SetUpPanel(PanelType.Item);
    }

    private void OpenEquipmentPanel()
    {
        OpenPanel(equipmentPanelController, equipmentButton);
        equipmentPanelController.CloseCandidateList();
        equipmentPanelController.SetupWithPartMeber(PartyManager.Instance.PartyMembers);
    }

    private void OpenPartyPanel()
    {
        OpenPanel(partyPanelController, partyButton);
    }

    private void OpenPanel(PanelController targetPanel, Button sourceButton)
    {
        if (targetPanel == null || sourceButton == null)
        {
            Debug.LogWarning("主菜单面板或对应按钮尚未在 Inspector 中绑定。", this);
            return;
        }

        if (_currentPanel != null && _currentPanel != targetPanel.gameObject)
            _currentPanel.SetActive(false);

        _lastButton = sourceButton;
        targetPanel.gameObject.SetActive(true);
        _currentPanel = targetPanel.gameObject;

        if (leftCanvas != null)
            leftCanvas.gameObject.SetActive(true);
    }

    private void UpdateCurrencyDisplayer()
    {
        if (currencyAmountText == null)
            return;

        var inventory = InventoryMananger.Instance;
        if (inventory == null)
            return;

        int currency = inventory.Currency;
        currencyAmountText.text = "持有金额：" + currency + "<b><color=yellow>金</color></b>";
    }
}

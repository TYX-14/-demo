
using TMPro;
using UnityEngine.UI;

public class MianPaneController : PanelController
{
    [Header("主菜单")]
    [SerializeField] private Button itemButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private TMP_Text currencyAmountText;

    [Header("面板")]
    [SerializeField] private ItemPanelControl itemPanelControl;
    [SerializeField] private EquipmentPanelController equipmentPanelController;
    [SerializeField] private CanvasGroup leftCanvas;

    private GameObject _currentPanel;
    private Button _lastButton;

    private void Awake()
    {
        _currentPanel = itemPanelControl.gameObject;
        RebindButtons(itemButton, OpenItemPanel);
        RebindButtons(equipmentButton, OpenEquipmentPanel);
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

    private void OpenPanel(PanelController equipmentPanel,Button sourceButton)
    {
        _currentPanel.SetActive(false);
        _lastButton = sourceButton;
        equipmentPanel.gameObject.SetActive(true);
        _currentPanel = equipmentPanel.gameObject;
        leftCanvas.gameObject.SetActive(true);

    }
    private void UpdateCurrencyDisplayer()
    {
        if (currencyAmountText == null)
        {
            return;
        }
        var inventery = InventoryMananger.Instance;
        int currency = inventery.Currency;
        currencyAmountText.text = "持有金额：" + currency + "<b><color=yellow>金</color></b>";
    }
}

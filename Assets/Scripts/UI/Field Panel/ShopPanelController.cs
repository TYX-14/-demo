
using System;
using TMPro;
using UnityEngine.UI;

public class ShopPanelController : PanelController
{
    [Header("买卖按钮与金额")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_Text currencyAmountText;

    [Header("中间区域")]
    [SerializeField] private ItemPanelControl itemPanel;

    [Header("交互区域")]
    [SerializeField] private CanvasGroup leftPart;
    [SerializeField] private CanvasGroup itemPanelCanvasGroup;

    [Header("popup")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TMP_Text popupTitle;
    [SerializeField] private TMP_Text popupText;

    [SerializeField] private Button popupConfirmButton;
    [SerializeField] private Button popupCancelButton;

    private PanelType _currentShopType;
    private ItemDefinitionSO _pendingItem;

    public override Type panelActionType => typeof(ShopAction);

    private ShopAction currentShopAction => (ShopAction)CurrentAction;

    private void Awake()
    {
        RebindButtons(buyButton, OpenBuyPanel);
        RebindButtons(sellButton, OpenSellPanel);

        RebindButtons(popupConfirmButton, ExecuteTransaction);
        RebindButtons(popupCancelButton, CloseConfirmPopup);
        confirmPopup.SetActive(false);
    }

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        UpdateCurrencyDisplayer();
        SetDefaultSelection();
    }

    private void OpenBuyPanel()
    {
        OpenItemPanel(PanelType.Buy);
    }
    private void SetBuyPopup(ItemDefinitionSO itemDefinition)
    {
        bool canBuy = InventoryMananger.Instance.Currency >= itemDefinition.BuyPrice;

        popupTitle.text = "确认要“购买”？：";
        if (canBuy) { popupText.text = itemDefinition.name + "\n价格：" + itemDefinition.BuyPrice + "<b><color=yellow>金</color></b>"; }
        else { popupText.text = itemDefinition.name+"（金币不足）" + "\n价格：" + itemDefinition.BuyPrice + "<b><color=yellow>金</color></b>"; }

        
        popupConfirmButton.gameObject.SetActive(canBuy);
        (canBuy ? popupConfirmButton : popupCancelButton).Select();

    }

    private void OpenSellPanel()
    {
        popupConfirmButton.gameObject.SetActive(true);
        OpenItemPanel(PanelType.Sell);
    }
    private void SetSellPanel(ItemDefinitionSO itemDefinition)
    {
        

        popupTitle.text = "确认要“出售”？：";
        popupText.text = itemDefinition.name + "\n价格：" + itemDefinition.SeltPrice + "<b><color=yellow>金</color></b>";

        popupCancelButton.interactable = true;
        popupConfirmButton.Select();
    }

    private void OpenItemPanel(PanelType panelType)
    {
        _currentShopType = panelType;
       
        itemPanel.gameObject.SetActive(true);
        itemPanel.SetUpPanel(panelType, OpenConfirmPopup, CurrentAction);
    }
    /// <summary>
    /// 打开popup,作为回调函数
    /// </summary>
    /// <param name="itemDefinition"></param>
    private void OpenConfirmPopup(ItemDefinitionSO itemDefinition)
    {
        _pendingItem = itemDefinition;
        confirmPopup.gameObject.SetActive(true);

        if (_currentShopType == PanelType.Buy)
        {
            SetBuyPopup(itemDefinition);
        }
        else if (_currentShopType == PanelType.Sell)
        {
            SetSellPanel(itemDefinition);
        }
    }
    private void CloseConfirmPopup()
    {
        confirmPopup.gameObject.SetActive(false);
        itemPanel.RecoreLastItemButtonSelection();
    }

    private void UpdateCurrencyDisplayer()
    {
        if (currencyAmountText == null)
        {
            return;
        }
        var inventery = InventoryMananger.Instance;
        int currency = inventery.Currency;
        currencyAmountText.text = "持有金额：" + currency+ "<b><color=yellow>金</color></b>";
    }
    /// <summary>
    /// 实现真实交易逻辑，//Action用来触发真实交易，itemPanel刷新持有数，如果卖光了，从itemPanel里面移除
    /// </summary>
    private void ExecuteTransaction()
    {
        //Action用来触发真实交易
        currentShopAction.TryExecuteTransaction(_currentShopType, _pendingItem);
        confirmPopup.gameObject.SetActive(false);
        //itemPanel刷新持有数
        UpdateCurrencyDisplayer();
        if (_currentShopType==PanelType.Buy)
        {
            itemPanel.RefrashItemQuantity(_pendingItem);
            
            
        }
        //如果卖光了，从itemPanel里面移除
        //TODO：装备不考虑
        if (_currentShopType == PanelType.Sell)
        {
            var remaining = EquipmentService.GetAvailableItemCount(InventoryMananger.Instance,PartyManager.Instance,_pendingItem);
            if (remaining > 0)
            {
                itemPanel.RefrashItemQuantity(_pendingItem);
                
                
            }
            else
            {
                if (itemPanel.RemoveItemButton(_pendingItem))
                {                
                    return;
                }
                else
                {
                    FirstButton = sellButton;
                    SetDefaultSelection();
                    return;
                }

                 
            }                      

        }        
        itemPanel.RecoreLastItemButtonSelection();
        
    }

}

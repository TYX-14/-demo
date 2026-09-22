
using System;

public class ItemPanelControl : PanelController
{
    [Header("Item Panel")]
    [SerializeField] private ItemButton ItemButtonPrefab;
    [SerializeField] private Transform itemButtonParent;

    private readonly List<ItemButton> itemButtons = new();
    private ItemButton currentItemButton;

    private PanelType _currentPanelType;
    private Action<ItemDefinitionSO> _onItemClick;

    


    private void Start()
    {
        
    }

    public void SetUpPanel(PanelType panelType,Action<ItemDefinitionSO> onItemClick=null, ActionBase actionBase = null)

    {
        if (actionBase != null)
        {
            base.SetupPanel(actionBase);
        }
        _currentPanelType = panelType;
        _onItemClick = onItemClick;

        ClearItemListView();

        var inventoryManger = InventoryMananger.Instance;
        switch (panelType)
        {
            case PanelType.Buy:
                BulidBuyList(inventoryManger); 
                break;
            case PanelType.Sell:
                BulidSellList(inventoryManger);
                break;
            case PanelType.Item:
                BulidItemList(inventoryManger);
                break;
        }
        if (itemButtons.Count == 0)
        {
            FirstButton = null;
            return;
        }
        FirstButton = itemButtons[0].CurrentButton;
        SetDefaultSelection();

    }


   
    private void ClearItemListView()
    {
        foreach (var itemButton in itemButtons)
        {
            Destroy(itemButton.gameObject);
        }
        itemButtons.Clear();
        FirstButton = null;
    }

    private void BulidBuyList(InventoryMananger inventory)
    {
        ShopAction shopAction = (ShopAction)CurrentAction;
        foreach (var item in shopAction.itemToBag)
        {
            int ownQuantity = inventory.GetItemQuantity(item);
            AddItemButton(new InventoryItem(item,ownQuantity));
        }
    }
    private void BulidSellList(InventoryMananger inventory)
    {
        //TODO:已装备物品不能被卖
        foreach (var item in inventory.CurrentInventory)
        {
            int availableForSell = EquipmentService.GetAvailableItemCount(inventory, PartyManager.Instance, item.ItemDefinition);

            if (availableForSell > 0)
            {
                AddItemButton(new InventoryItem(item.ItemDefinition, availableForSell));
                continue;
            }

            if (availableForSell == 0 && item.ItemDefinition != null && item.ItemDefinition.ItemType == ItemType.Equipment&&item.Quantity == 1)
            {
                AddItemButton(new InventoryItem(item.ItemDefinition,0),false,true);
            }

            
        }
    }

    private void BulidItemList(InventoryMananger inventory)
    {
        foreach (var item in inventory.CurrentInventory)
        {
            AddItemButton(item);
        }
    }

    private void AddItemButton(InventoryItem inventoryItem, bool interactable=true, bool equippedNameFormat = false)
    {
        ItemButton itemButton = Instantiate(ItemButtonPrefab, itemButtonParent);
        if (itemButton is ShopItemButton shopItemButton)
        {
            shopItemButton.SetupButton(inventoryItem, _currentPanelType, _onItemClick);
        }
        else
        {
            itemButton.SetupButton(inventoryItem, _onItemClick);
        }

        if (equippedNameFormat)
        {
            itemButton.SetEquippedNameFormat();
        }

        itemButton.CurrentButton.interactable = interactable;
        itemButton.CurrentButton.onClick.AddListener(() => OnAnyItemButtonClick(itemButton));
        itemButtons.Add(itemButton);
    }

    public void RefrashItemQuantity(ItemDefinitionSO itemDefinition)
    {
        foreach (var itemButton in itemButtons)
        {
            if (itemDefinition = itemButton.ItemDefinition)
            {
                int newQuantity = InventoryMananger.Instance.GetItemQuantity(itemDefinition);
                itemButton.UpdateQuantity(newQuantity);
                break;
            }
        }
    }
    /// <summary>
    /// 返回bool：如果返回fasle，说明Itembuttons所有元素被移除
    /// </summary>
    /// <param name="itemDefinition"></param>
    /// <returns></returns>
    internal bool RemoveItemButton(ItemDefinitionSO itemDefinition)
    {
        
        int targetIndex = itemButtons.FindIndex(itembuttom => itembuttom.ItemDefinition == itemDefinition);
        ItemButton targetButton = itemButtons[targetIndex];
        itemButtons.RemoveAt(targetIndex);
        Destroy(targetButton.gameObject);
        
        if (itemButtons.Count <= 0)
        {
            
            
            return false;
        }

        SetItemButtonSelection(itemButtons[0]);
        return true;

    }

    /// <summary>
    /// 让ItemButto知道他自己已经被点击的监听
    /// </summary>
    private void OnAnyItemButtonClick(ItemButton itemButton)
    {
        currentItemButton = itemButton;
    }
    /// <summary>
    /// 用于在取消某个面板过后，恢复上一个Itembutton的选中
    /// </summary>
    public void RecoreLastItemButtonSelection()
    {
        //如果按钮已经没了（比如玩家卖光了） 则返回
        
        if (itemButtons == null || itemButtons.Count <= 0)
        {
            
           
            return;
        }

        
        SetItemButtonSelection(currentItemButton);
        
    }
}

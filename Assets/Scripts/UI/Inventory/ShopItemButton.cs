
using System;
using TMPro;
using Unity.VisualScripting;

public class ShopItemButton : ItemButton
{
    [Header("商店独有")]
    [SerializeField] private TMP_Text priceText;


    public void SetupButton(InventoryItem inventoryItem,PanelType panelType, Action<ItemDefinitionSO> onItemClick)
    {
        base.SetupButton(inventoryItem,onItemClick);
        
        priceText.text = panelType == PanelType.Buy
            ? inventoryItem.ItemDefinition.BuyPrice + "<b><color=yellow>金</color></b>"
            : inventoryItem.ItemDefinition.SeltPrice + "<b><color=yellow>金</color></b>";


    }
}

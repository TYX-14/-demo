
using System;
using TMPro;

public class StealItemButton : ItemButton
{
    [Header("Õµ ŒÔ∆∑∞¥≈•")]
    [SerializeField] private TMP_Text rateText;

    public override void SetupButton(InventoryItem inventoryItem, Action<ItemDefinitionSO> onItemClick)
    {
        base.SetupButton(inventoryItem, onItemClick);
        rateText.text = _currentItem.ItemDefinition.RaritWeight.ToString()+"%";
    }


}

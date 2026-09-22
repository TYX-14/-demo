
public class InventoryMananger : singleton<InventoryMananger>
{
    [Header("图标设置")]
    public ItemIconSetSO IconSet;

    [Header("库存")]
    public List<InventoryItem> CurrentInventory = new();
    [Header("持有金币")]
    public int Currency;
    private int _initialCurrenct;
    public void AddItem(ItemDefinitionSO itemDefinition, int quantity)
    {
        foreach (var item in CurrentInventory)
        {
            if (item.ItemDefinition != itemDefinition)
            {
                continue;
            }
            item.Quantity += quantity;
            return;
        }
        CurrentInventory.Add(new InventoryItem(itemDefinition, quantity));
    }

    public void RemoveItem(ItemDefinitionSO itemDefinition,int quantity)
    {
        for (int i = 0; i < CurrentInventory.Count; i++)
        {
            if (CurrentInventory[i].ItemDefinition != itemDefinition)
            {
                continue ;
            }
            CurrentInventory[i].Quantity -= quantity;

            if (CurrentInventory[i].Quantity <= 0)
            {
                CurrentInventory.RemoveAt(i);
            }
            return;
        }
    }

    public int GetItemQuantity(ItemDefinitionSO itemDefinition)
    {
        if (itemDefinition == null)
        {
            return 0;
        }

        foreach (var item in CurrentInventory)
        {
            if (item.ItemDefinition == itemDefinition)
            {
                return item.Quantity;
            }

        }
        return 0;   
    }

    public bool TrySpednCurrency(int amount)
    {
        if (Currency < amount)
        {
            return false;
        }
        else
        {
            Currency -= amount;
            return true;
        }
    }

    public void AddCurrency(int sellPrice)
    {
        Currency += sellPrice;
    }
}

[System.Serializable]
public class InventoryItem
{
    public ItemDefinitionSO ItemDefinition;
    public int Quantity;

    public InventoryItem(ItemDefinitionSO itemDefinition, int quantity)
    {
        ItemDefinition = itemDefinition;
        Quantity = quantity;
    }

    public bool IsEquiupment => ItemDefinition != null && ItemDefinition.ItemType == ItemType.Equipment;
    public bool IsConsumable => ItemDefinition != null && ItemDefinition.ItemType == ItemType.Consumable;

}

public class ShopAction : ActionBase
{
    [Header("商店特有")]
    public List<ItemDefinitionSO> itemToBag;
    public override void TriggerAction(AllyDefinitionSO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));

    }
    /// <summary>
    /// 具体实现交易逻辑，在ShopPenelController中调用
    /// </summary>
    /// <param name="panelType"></param>
    /// <param name="itemDefinition"></param>
    /// <returns></returns>
    public bool TryExecuteTransaction(PanelType panelType, ItemDefinitionSO itemDefinition)
    {
        var inventory = InventoryMananger.Instance;
        if (panelType == PanelType.Buy)
        {
            if (!inventory.TrySpednCurrency(itemDefinition.BuyPrice))
            {
                return false;
            }
            inventory.AddItem(itemDefinition, 1);
            return true;
        }
        if (panelType == PanelType.Sell)
        {
            if (inventory.GetItemQuantity(itemDefinition) <= 0)
            {
                return false;
            }
            inventory.RemoveItem(itemDefinition, 1);
            inventory.AddCurrency(itemDefinition.SeltPrice);
            return true;
        }
        return false;
    }
}

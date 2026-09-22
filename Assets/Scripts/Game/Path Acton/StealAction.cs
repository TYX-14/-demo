
public class StealAction : ActionBase
{
    [Header("͵ȡaction")]
    public List<InventoryItem> itemToSteal;

    public override void TriggerAction(AllyDefinitionSO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));

    }

    public bool TrySteal(ItemDefinitionSO itemDefinition)
    {
        bool success = Random.Range(1,100)<=itemDefinition.RaritWeight;

        if (success)
        {
            InventoryMananger.Instance.AddItem(itemDefinition, 1);
            for (int i = 0; i < itemToSteal.Count; i++)
            {
                if (itemDefinition != itemToSteal[i].ItemDefinition)
                {
                    continue;
                }
                itemToSteal.RemoveAt(i);
            }
        }
        return success;
    }
}

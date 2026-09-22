[CreateAssetMenu(menuName = "Inventory/Cosumable Item")]
public class ConsumableItemSO : ItemDefinitionSO
{
    [Header("消耗品特有")]
    [Min(0)] public int restoreAmount;
}

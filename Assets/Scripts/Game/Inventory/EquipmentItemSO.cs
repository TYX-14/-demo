[CreateAssetMenu(menuName = "Inventory/Equipment Item")]
public class EquipmentItemSO : ItemDefinitionSO
{
    [Header("◊∞±∏≈‰÷√")]
    public EquipmentCategory category = EquipmentCategory.Weapon;
    public WeaqonType weaponType = WeaqonType.Sword;

    public StatBlock statBonus = StatBlock.Zero;




    private void OnValidate()
    {
        if (category != EquipmentCategory.Weapon)
        {
            weaponType = WeaqonType.None;
        }
    }
}

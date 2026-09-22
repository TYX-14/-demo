[CreateAssetMenu(menuName = "Inventory/ItemIconSet")]
public class ItemIconSetSO : ScriptableObject
{ 
    public ItemIconEntry[] itemIcons;

    public Sprite GetIcon(ItemIconKey iconKey)
    {
        foreach (var item in itemIcons)
        {
            if (item.itemIconKey == iconKey)
            {
                return item.icon;
            }
        }
        return null;    
    }
}


[System.Serializable]

public class ItemIconEntry
{
    public ItemIconKey itemIconKey;
    public Sprite icon;
}
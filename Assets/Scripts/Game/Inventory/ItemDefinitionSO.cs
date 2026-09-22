
public class ItemDefinitionSO : ScriptableObject
{
    public string ItemName;
    [TextArea] public string ItemDescription;
    public ItemType ItemType;

    public ItemIconKey ItemIconKey;

    public int BuyPrice;
    public int SeltPrice =>(int)(BuyPrice*0.75);
    public int MaxStack = 99;

    [Header("Ï¡ÓÐ¶È")]
    public int RaritWeight = 100;

}

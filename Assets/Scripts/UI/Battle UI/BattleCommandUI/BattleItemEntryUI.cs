using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗道具列表中的单个条目。
/// 只展示道具图标、名称和持有数量，不在此处执行使用效果。
/// </summary>
public class BattleItemEntryUI : MonoBehaviour
{
    [Header("可在 Inspector 中分别绑定")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text quantityText;

    public void Setup(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || inventoryItem.ItemDefinition == null)
        {
            Clear();
            return;
        }

        ItemDefinitionSO definition = inventoryItem.ItemDefinition;

        if (itemIcon != null)
        {
            ItemIconSetSO iconSet = InventoryMananger.Instance != null
                ? InventoryMananger.Instance.IconSet
                : null;

            itemIcon.sprite = iconSet != null
                ? iconSet.GetIcon(definition.ItemIconKey)
                : null;
            itemIcon.enabled = itemIcon.sprite != null;
        }

        if (itemNameText != null)
            itemNameText.text = definition.ItemName;

        if (quantityText != null)
            quantityText.text = $"×{inventoryItem.Quantity}";
    }

    private void Clear()
    {
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }

        if (itemNameText != null)
            itemNameText.text = string.Empty;

        if (quantityText != null)
            quantityText.text = string.Empty;
    }
}

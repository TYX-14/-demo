using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战斗道具列表中的单个条目。
/// 显示道具图标、名称和持有数量，并在鼠标悬停时展示物品说明。
/// </summary>
public class BattleItemEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("可在 Inspector 中分别绑定")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text quantityText;

    private GameObject itemTip;
    private TMP_Text itemTipDescription;

    private void Awake()
    {
        // Tip 已经放在按钮预制体下，按名称自动获取，避免额外改动预制体引用。
        Transform tipTransform = transform.Find("Item Tip");
        if (tipTransform == null)
            tipTransform = FindChildByName(transform, "Item Tip");

        if (tipTransform != null)
        {
            itemTip = tipTransform.gameObject;
            itemTipDescription = itemTip.GetComponentInChildren<TMP_Text>(true);
            itemTip.SetActive(false);

            // Tip 只用于展示，不能挡住按钮的指针事件，否则悬停时可能闪烁。
            foreach (Graphic graphic in itemTip.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

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

        if (itemTipDescription != null)
            itemTipDescription.text = definition.ItemDescription ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemTip != null)
            itemTip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (itemTip != null)
            itemTip.SetActive(false);
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
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

        if (itemTipDescription != null)
            itemTipDescription.text = string.Empty;

        if (itemTip != null)
            itemTip.SetActive(false);
    }
}

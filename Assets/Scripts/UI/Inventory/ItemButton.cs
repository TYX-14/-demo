using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class ItemButton : MonoBehaviour, ISelectHandler, IDeselectHandler
{

    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemQuantity;
    [SerializeField] private GameObject itemTips;


    protected Button _button;
    public Button CurrentButton => _button;

   

    protected InventoryItem _currentItem;
    public ItemDefinitionSO ItemDefinition => _currentItem.ItemDefinition;

    private Action<ItemDefinitionSO> _onItemClick;

    protected virtual void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    public virtual void SetupButton(InventoryItem inventoryItem)=>SetupButton(inventoryItem,null);

    public virtual void SetupButton(InventoryItem inventoryItem, Action<ItemDefinitionSO> onItemClick)
    {
        _currentItem = inventoryItem;
        _onItemClick = onItemClick;

        itemIcon.sprite = InventoryMananger.Instance.IconSet.GetIcon(inventoryItem.ItemDefinition.ItemIconKey);
        itemName.text = inventoryItem.ItemDefinition.ItemName;
        itemDescription.text = inventoryItem.ItemDefinition.ItemDescription;

        if (itemQuantity != null)
        {
            itemQuantity.text = inventoryItem.Quantity.ToString();
        }
    }

    public void SetEquippedNameFormat()
    {
        if (_currentItem.ItemDefinition != null)
        {
            itemName.text = _currentItem.ItemDefinition.ItemName + "(×°±¸ÖÐ)";
        }
    }
        
    protected virtual void OnClick()
    {
        if (_onItemClick != null)
        {
            _onItemClick.Invoke(_currentItem.ItemDefinition);
        }
        
    }

    public void UpdateQuantity(int quantity)
    {
        if (itemQuantity != null)
        {
            itemQuantity.text = quantity.ToString();
        }
    }
   


    public void OnDeselect(BaseEventData eventData)
    {
        itemTips.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        itemTips.SetActive(true);
    }
}

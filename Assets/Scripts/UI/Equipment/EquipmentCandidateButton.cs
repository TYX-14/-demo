using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System;
public class EquipmentCandidateButton : MonoBehaviour,ISelectHandler
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text ownedCountText;

    [Header("Color")]
    [SerializeField] private Color normalTextColor;
    [SerializeField] private Color disableTextColor;

    private Button _button;
    private int _index;
    public Action<int> OnSelect;
    public Action<int> OnClick;

    public Button Button => _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClick);
    }

    public void Setup(int index, EquipmentItemSO item, int owendCount, bool isInteractable, ItemIconSetSO iconSet)
    {
       
        _index = index;
        _button.interactable = isInteractable;
        itemNameText.text = item != null ? item.ItemName : "ÎÞ×°±¸";
        ownedCountText.text = item != null ? owendCount.ToString() : "-";
        if (item != null)
        {

            itemIcon.sprite = iconSet.GetIcon(item.ItemIconKey);
            itemIcon.enabled = true;
        }
        else
        {
            itemIcon.sprite = null;
            itemIcon.enabled=false;
        }
        itemNameText.color = isInteractable ? normalTextColor : disableTextColor;
        ownedCountText.color = !isInteractable ? normalTextColor : disableTextColor;
    }

    void ISelectHandler.OnSelect(BaseEventData eventData)
    {
        if (!_button.interactable) return;
        
        OnSelect?.Invoke(_index);
    }

    private void HandleClick()
    {
        if (!_button.interactable) return ;
        OnClick?.Invoke(_index);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StealPanelController : PanelController
{
    [Header("偷取面板")]
    [SerializeField] public StealItemButton stealItemButtonPrefab;
    [SerializeField] private RectTransform contentRoot;

    [Header("popup")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TMP_Text popupText;

    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public override Type panelActionType => typeof(StealAction);
    private readonly List<StealItemButton> _stealItemButtons = new List<StealItemButton>();
    private StealAction CurrentStealAction => (StealAction)CurrentAction;

    private ItemDefinitionSO _pendingItem;

   

    public override void OnConfirm()
    {
        bool suceess = CurrentStealAction.TrySteal(_pendingItem);
        popupText.text = suceess ? "偷取成功" : "偷取失败";
        cancelButton.gameObject.SetActive(false);
        RebindButtons(confirmButton,ClosePopup);

    }
    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        confirmPopup.gameObject.SetActive(false);

        RefreshItemList();
       
    }
    /// <summary>
    /// 刷新物品列表
    /// </summary>
    private void RefreshItemList()
    {
        ClearItemList();

        foreach (InventoryItem item in CurrentStealAction.itemToSteal)
        {
            StealItemButton stealItemButton = Instantiate(stealItemButtonPrefab, contentRoot);
            stealItemButton.SetupButton(item, OpenConfirmPopup);
            _stealItemButtons.Add(stealItemButton);

        }
        if (_stealItemButtons.Count == 0) return;
        FirstButton = _stealItemButtons[0].CurrentButton;
        SetDefaultSelection();
    }
    /// <summary>
    /// 作为Action回调函数使用
    /// </summary>
    /// <param name="itemDefinition"></param>
    private void OpenConfirmPopup(ItemDefinitionSO itemDefinition)
    {
        _pendingItem = itemDefinition;
        FirstButton = confirmButton;

        confirmPopup.gameObject.SetActive(true);

        popupText.text = itemDefinition.name + " 成功率：" + itemDefinition.RaritWeight +"%";

        RebindButtons(confirmButton, OnConfirm);
        RebindButtons(cancelButton, ClosePopup);
        
        SetDefaultSelection();
    }


    private void ClosePopup()
    {
        HidePopup();
        RefreshItemList();
    }

    private void HidePopup()
    {
        _pendingItem = null;
        confirmPopup.gameObject.SetActive(false);
    }
    public void ClearItemList()
    {
        foreach (StealItemButton stealItemButton in _stealItemButtons)
        {
            Destroy(stealItemButton.gameObject);
        }
        _stealItemButtons.Clear();
        FirstButton = null;
    }

    

}

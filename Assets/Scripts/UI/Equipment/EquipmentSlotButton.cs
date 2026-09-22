

using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlotButton : MonoBehaviour, ISelectHandler,IDeselectHandler
{
    [SerializeField] TMP_Text slotNameText;
    [SerializeField] Image slotIconImage;
    private CanvasGroup canvasGroup;
    [SerializeField] private Button _button;

    private int _index;

    private Action<int> _onSelect;
    private Action<int> _onCliked;

    public Button Button =>_button;
    

    private bool _isSlotUsable=true;//插槽是否可用
    private bool _isInpuntEnale=false;//是否可交互
    private Navigation _defaulNavigation;

    public Sprite SlotIconsprite => slotIconImage.sprite;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        _defaulNavigation = Button.navigation;
    }


    private void OnEnable()
    {
          _button.onClick.AddListener(HandleClick);
    }
    private void OnDisable()
    {
        _button?.onClick.RemoveAllListeners();
    }
    /// <summary>
    /// 设置slot，该方法内置判定slot的可用性
    /// </summary>
    /// <param name="equipmentItem"></param>
    /// <param name="index"></param>
    /// <param name="onSelect"></param>
    /// <param name="onCliked"></param>
    /// <param name="isSlotUsable"></param>
    public void SetUp(
        EquipmentItemSO equipmentItem,
        int index,
        Action<int> onSelect,
        Action<int> onCliked,
        bool isSlotUsable
        )
    {
        _index = index;
        _onSelect = onSelect;
        _onCliked = onCliked;
        _isSlotUsable = isSlotUsable;

        // 设置显示名称，若无装备则显示"未装备"
        string displayName = equipmentItem != null ? equipmentItem.ItemName : "未装备";
        slotNameText.text = displayName;

        // 根据槽位可用状态更新按钮交互表现
        ApplyButtonInteractableState();
    }

    /// <summary>
    /// 实现button的可选性
    /// </summary>
    private void ApplyButtonInteractableState()
    {
        bool isInteractable = _isSlotUsable;
        Button.interactable = isInteractable;

        // 控制 UI 渐隐和是否可响应点击
        canvasGroup.alpha = _isSlotUsable ? 1 : 0;
        canvasGroup.interactable = isInteractable;

        // 处理 UI 的导航（手柄/键盘选择逻辑）
       ApplyButtonNavigation();
    }
/// <summary>
/// 设置button导航
/// </summary>
    public void ApplyButtonNavigation()
    {
        //先恢复导航
        Button.navigation = _defaulNavigation;


        bool isInteractable = _isSlotUsable;
        Navigation navigation = Button.navigation;
        if (!isInteractable)
        {
            navigation.mode = Navigation.Mode.None;
        }
        Button.navigation = navigation;
    }


    private void HandleClick()
    {
        
        _onCliked?.Invoke(_index);
    }

    public void OnSelect(BaseEventData eventData)
    {
        _onSelect?.Invoke(_index);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        
    }
}

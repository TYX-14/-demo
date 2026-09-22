

using System;
using TMPro;
using UnityEngine.UI;

public class EquipmentCandidatePaneControllerl : MonoBehaviour
{
    [Header("[Header]")]
    [SerializeField] private TMP_Text slotNameText;
    [SerializeField] private Image slotIconImage;

    [Header("List")]
    [SerializeField] private RectTransform candidateListRoot;
    [SerializeField] private EquipmentCandidateButton candidateButtonPrefab;

    // 当前面板上的按钮列表
    private readonly List<EquipmentCandidateButton> _buttons = new();
    // 当前候选装备列表
    private readonly List<EquipmentItemSO> _candidates = new();

    private Button _leftNeighbor;
    private Button _rightNeighbor;

    //逐层传递事件
    public event Action<int> OnSelectedIndexChanged;
    public event Action<int> OnCandidateClicked;

    /// <summary>
    /// 设置左右相邻按钮，用于跨面板导航
    /// </summary>
    public void SetHorizontalNeighbors(Button left, Button right =null)
    {
        _leftNeighbor = left;
        _rightNeighbor = right;
    }
    /// <summary>
    /// 打开面板，显示指定装备槽的候选装备
    /// </summary>
    public void OpenForSlot(EquipSlot slot, CharacterRuntimeData member, string slotDisplayName, Sprite slotIcon)
    {
        RefreshHeader(slotDisplayName, slotIcon);
        RebuildCandidates(slot);
    }

    /// <summary>
    /// 根据装备槽动态生成候选按钮
    /// </summary>
    private void RebuildCandidates(EquipSlot slot)
    {
        _candidates.Clear();
        ClearButtons();

        InventoryMananger inventory = InventoryMananger.Instance;
        PartyManager partyManager = PartyManager.Instance;

        // 构建当前槽可用装备列表
        _candidates.AddRange(EquipmentService.BuildCandidates(inventory, slot));

        for (int i = 0; i < _candidates.Count; i++)
        {
            int availableCount = EquipmentService.GetAvailableItemCount(inventory, partyManager, _candidates[i]);

            // 实例化按钮
            EquipmentCandidateButton candidateButton = Instantiate(candidateButtonPrefab, candidateListRoot);

            // 判断按钮是否可交互
            bool isInteractable = _candidates[i] == null || availableCount > 0;

            candidateButton.Setup(i, _candidates[i], availableCount, isInteractable, inventory.IconSet);

            // 添加到按钮列表
            _buttons.Add(candidateButton);

            candidateButton.OnClick += HandleCandidateClicked;
            candidateButton.OnSelect += HandleCandidateSelected;
        }

        // 在生成完所有按钮后，设置导航（上下不越界）
        SetupNavigation();
    }

    /// <summary>
    /// 刷新面板头部显示
    /// </summary>
    private void RefreshHeader(string slotDisplayName, Sprite slotIcon)
    {
        slotNameText.text = slotDisplayName;
        slotIconImage.sprite = slotIcon;
        slotIconImage.enabled = slotIcon != null;
    }

    /// <summary>
    /// 清理旧按钮
    /// </summary>
    private void ClearButtons()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (_buttons[i] != null)
                Destroy(_buttons[i].gameObject);
        }
        _buttons.Clear();
    }

    /// <summary>
    /// 给动态生成的按钮设置导航，确保上下切换不会越界
    /// </summary>
    private void SetupNavigation()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            Navigation nav = _buttons[i].Button.navigation;
            nav.mode = Navigation.Mode.Explicit;

            // 上按钮：如果是第一个按钮，按上不变
            nav.selectOnUp = i == 0 ? _buttons[i].Button : _buttons[i - 1].Button;
            // 下按钮：如果是最后一个按钮，按下不变
            nav.selectOnDown = i == _buttons.Count - 1 || !_buttons[i + 1].Button.interactable ? _buttons[i].Button : _buttons[i + 1].Button;


            // 左右按钮：如果外部有邻居面板按钮则跳转，否则保持当前按钮
            nav.selectOnLeft = _leftNeighbor != null ? _leftNeighbor : _buttons[i].Button;
            nav.selectOnRight = _rightNeighbor != null ? _rightNeighbor : _buttons[i].Button;

            

            _buttons[i].Button.navigation = nav;
        }
    }

    private void HandleCandidateSelected(int index)
    {
       OnSelectedIndexChanged?.Invoke(index);
    }

    private void HandleCandidateClicked(int index)
    {
        OnCandidateClicked?.Invoke(index);
    }

    internal EquipmentItemSO GetCandidate(int candidateIndex)
    {
       return _candidates[candidateIndex];
    }
}


using System;
using TMPro;
using UnityEngine.UI;
using static UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle;
using static UnityEngine.Rendering.DebugUI;

public class EquipmentPanelController :  PanelController
{
    [Header("Tabs")]
    [SerializeField] private RectTransform tabRoot;
    [SerializeField] private CanvasGroup tabCanvas;
    [SerializeField] private EquipmentCharacterTabButton tabPrefab;

    [Header("装备槽")]
    [SerializeField] private EquipmentSlotButton[] slotButtons;
    [SerializeField] private CanvasGroup slotListCanvasGroup;

    [Header("人物名字")]
    [SerializeField] private TMP_Text memberNameText;
    
    [Header("左侧装备Type")]
    [SerializeField] private TMP_Text[] leftCategoryNameTexts;
    [SerializeField] private Color leftCategoryEnableColor = Color.black;
    [SerializeField] private Color leftCategoryDisableColor = new Color(0.72f, 0.72f, 0.72f);

    [Header("装备选择面板")]
    [SerializeField] private EquipmentCandidatePaneControllerl CandidatePanel;

    [Header("Stat面板")]
    [SerializeField] private EquipmentStatPreviewPanel statPreviewPanel;

    #region 运行缓存
    private List<EquipmentCharacterTabButton> _tabButtons = new();
    private List<CharacterRuntimeData> _partyMeber = new();
    private int _meberIndex;
    private int _slotIndex;
   

    /// <summary>
    /// 通过_meberIndex确认当前选择的角色
    /// </summary>
    private CharacterRuntimeData _currentMember =>
        _meberIndex>=0 && _meberIndex<_partyMeber.Count ? _partyMeber[_meberIndex] : null;

    #endregion


    #region 代码周期
    private void Awake()
    {
        CandidatePanel.OnSelectedIndexChanged += OnCandidateSelected;
        CandidatePanel.OnCandidateClicked += OnCandidateClicked;
    }

    private void OnDestroy()
    {
        CandidatePanel.OnCandidateClicked -= OnCandidateClicked;
        CandidatePanel.OnSelectedIndexChanged -= OnCandidateSelected;
    }

    #endregion

    public void SetupWithPartMeber(List<CharacterRuntimeData> partyMeber)
    {
        _partyMeber.Clear();
        
        for (int i = 0; i < partyMeber.Count; i++)
        {
            var meber = partyMeber[i];
            if (meber == null) continue;
            //TODO:绑定人物属性
            _partyMeber.Add(meber);
        }
        BulidCharacterTabs();
        RefreshCurrentMemberView();
    }

    private void BulidCharacterTabs()
    {
        ClearTabButton();
        for (int i = 0; i<_partyMeber.Count;i++)
        {
            var tab = Instantiate(tabPrefab,tabRoot);
            tab.SetUp(_partyMeber[i],i,OnTabSelected,OnTabClick);
            
            _tabButtons.Add(tab);
        }
        FirstButton = _tabButtons[0].Button;
        SetDefaultSelection();
    }

    /// <summary>
    /// 通过人物，刷新面板
    /// </summary>
    private void RefreshCurrentMemberView()
    {
        memberNameText.text = _currentMember.DisplayName;

        for (int i = 0; i < FixedSlotOrder.Length; i++)
        {
            EquipmentSlotButton slotButton = slotButtons[i];
            EquipSlot slot = FixedSlotOrder[i];
            EquipmentItemSO equipedItem = _currentMember.GetEquippedItem(slot);

            slotButton.gameObject.SetActive(true);
            slotButton.SetUp(equipedItem, i, OnSlotSelected, OnSlotClicked, IsSlotUsableForMember(_currentMember, i));
        }
        //todo对比
        statPreviewPanel.Refresh(_currentMember.GetTotalStats(), _currentMember.GetTotalStats(), false);
        RefresLeft(_currentMember);
    }
    /// <summary>
    /// 判断人物是否可以装备该类型武器
    /// </summary>
    /// <returns></returns>
    private bool IsSlotUsableForMember(CharacterRuntimeData member,int slotIndex)
    {
        if(slotIndex>=weaqonTypes.Length)return true;

        return member.Definition is AllyDefinitionSO allyDef && allyDef.CanEquipWeaponType(weaqonTypes[slotIndex]);
    }
    /// <summary>
    /// 设置左边slot的透明度
    /// </summary>
    private void RefresLeft(CharacterRuntimeData member)
    {
        for (int i = 0; i < leftCategoryNameTexts.Length; i++)
        {
            leftCategoryNameTexts[i].color = IsSlotUsableForMember(_currentMember,i)
                ? leftCategoryEnableColor : leftCategoryDisableColor;

        }
    }
    /// <summary>
    /// 更新能力值变化
    /// </summary>
    public void UpdateCandidatePreview(int candidateIndex)
    {
        EquipSlot slot = FixedSlotOrder[_slotIndex];
        EquipmentItemSO previewItem = CandidatePanel.GetCandidate(candidateIndex);
        
        RefreshPreview(slot, previewItem, true);
        
    }

    private void RefreshPreview(EquipSlot slot,EquipmentItemSO previewItem,bool isInPreviewMode)
    {
        var currentTotal = _currentMember.GetTotalStats();
        var previewTotal = EquipmentService.BuildPreviewTotalStats(_currentMember,slot,previewItem);
        
        statPreviewPanel.Refresh(currentTotal,previewTotal,isInPreviewMode);
        
    }

    #region Tab选中点击逻辑
    private void OnTabClick(int index)
    {

       
        //切换人物
    }
    private void OnTabSelected(int index)
    {
        _meberIndex = index;
        
        RefreshCurrentMemberView();
    }
    #endregion
    #region Slot选中点击逻辑
    private void OnSlotClicked(int index)
    {
        OnSlotSelected(index);
       
        CandidatePanel.gameObject.SetActive(true);
    }

    private void OnSlotSelected(int index)
    {
      if(_currentMember == null || statPreviewPanel == null)
        {
            return;
        }
      _slotIndex = index;
        statPreviewPanel.Refresh(_currentMember.GetTotalStats(), _currentMember.GetTotalStats(), false);
        OpenCandidateList(index);
    }
    #endregion
    #region Candidate选中点击逻辑
    private void OnCandidateSelected(int index)
    {
        UpdateCandidatePreview(index);
        
    }

    private void OnCandidateClicked(int index)
    {
        var slot = FixedSlotOrder[_slotIndex];
        EquipmentItemSO targetItem = CandidatePanel.GetCandidate(index);
        if (_currentMember.GetEquippedItem(slot) == targetItem)
        {
            return ;
        }

        _currentMember.SetEquippedItem(slot, targetItem);
        RefreshCurrentMemberView();
        OpenCandidateList(_slotIndex);

        slotButtons[_slotIndex].Button.Select();
       
    }
    #endregion

    private void ClearTabButton()
    {
        for (int i = 0; i<_tabButtons.Count;i++)
        {
            Destroy(_tabButtons[i].gameObject);
        }
        _tabButtons.Clear();
    }

    private void ClampMemberIndex()
    {

        _meberIndex =_tabButtons.Count>0
            ? Mathf.Clamp(_meberIndex, 0, _partyMeber.Count - 1)
            :0;
    }
  

    #region 固定槽位
    private static readonly WeaqonType[] weaqonTypes =
        {
            WeaqonType.Sword,
            WeaqonType.Spear,
            WeaqonType.Dagger,
            WeaqonType.Axe,
            WeaqonType.Bow,
            WeaqonType.Staff
        };
    private static readonly EquipSlot[] FixedSlotOrder =
        {
            EquipSlot.Sword,
            EquipSlot.Spear,
            EquipSlot.Dagger,
            EquipSlot.Axe,
            EquipSlot.Bow,
            EquipSlot.Staff,
            EquipSlot.Shield,
            EquipSlot.Head,
            EquipSlot.Body,
            EquipSlot.Accessory1,
            EquipSlot.Accessory2
        };

    #endregion

    #region 选择装备面板
    private void OpenCandidateList(int slotIndex)
    {
        CandidatePanel.gameObject.SetActive(true);
        
        CandidatePanel.SetHorizontalNeighbors(slotButtons[_slotIndex].Button);

        

        EquipSlot slot = FixedSlotOrder[slotIndex];
        string slotName = leftCategoryNameTexts[slotIndex].text;
        CandidatePanel.OpenForSlot(slot, _currentMember, slotName, slotButtons[slotIndex].SlotIconsprite);
        
        print(_slotIndex);
    }

    public void CloseCandidateList()
    {
        CandidatePanel?.gameObject.SetActive(false);
    }
    #endregion
}

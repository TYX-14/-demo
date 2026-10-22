using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 编队面板控制器。
/// 队伍区域使用四个固定槽位，英雄名册使用可滚动列表。
/// 角色条目的名字、等级、血量和头像由 PartyFormationEntryView 分别绑定。
/// </summary>
public class PartyFormationPanelController : MonoBehaviour
{
    [Header("四个固定队伍槽位，按 1 至 4 号位排列")]
    [SerializeField] private PartyFormationEntryView[] partySlots = new PartyFormationEntryView[4];

    [Header("英雄名册滚动列表")]
    [SerializeField] private Transform rosterContent;

    [Tooltip("预制体根对象需要挂载 PartyFormationEntryView，并分别绑定名字、等级、血量、头像。")]
    [SerializeField] private PartyFormationEntryView rosterEntryPrefab;

    [Header("英雄详情")]
    [SerializeField] private TMP_Text characterDetailsText;

    [Header("可选状态提示")]
    [SerializeField] private TMP_Text statusText;

    private readonly List<GameObject> spawnedRosterEntries = new();

    private Canvas dragCanvas;
    private RectTransform dragPreviewRect;
    private Image dragPreviewImage;
    private CharacterRuntimeData draggedMember;

    private void OnEnable()
    {
        RefreshView();
    }

    private void OnDisable()
    {
        HideDragPreview();
    }

    /// <summary>
    /// 重新读取管理器中的数据并刷新固定槽位、英雄名册和详情。
    /// </summary>
    public void RefreshView()
    {
        ClearRosterEntries();

        PartyManager partyManager = PartyManager.Instance;
        CharacterRosterManager rosterManager = CharacterRosterManager.Instance;

        if (partyManager == null || rosterManager == null)
        {
            SetStatus("无法读取队伍或英雄名册，请检查场景中的管理器。");
            ClearPartySlots();
            return;
        }

        if (partySlots == null || partySlots.Length != 4 ||
            rosterContent == null || rosterEntryPrefab == null)
        {
            SetStatus("请绑定 4 个队伍槽位、英雄名册 Content 和条目预制体。");
            return;
        }

        EnsureRosterLayout();

        List<CharacterRuntimeData> party = partyManager.PartyMembers;
        IReadOnlyList<CharacterRuntimeData> roster = rosterManager.OwnedCharacters;
        bool isBattleMode = GameModeManager.Instance != null &&
                            GameModeManager.Instance.CurrentGameMode == GameMode.Battle;

        for (int i = 0; i < partySlots.Length; i++)
        {
            PartyFormationEntryView slotView = partySlots[i];
            if (slotView == null)
                continue;

            Button slotButton = slotView.EntryButton;
            int position = i + 1;
            slotView.ConfigureInteraction(this, position);
            CharacterRuntimeData member = partyManager.GetMemberAtPosition(position);

            slotButton.onClick.RemoveAllListeners();

            if (member == null)
            {
                slotView.SetEmpty($"位置 {position}");
                slotButton.interactable = false;
                continue;
            }

            slotView.SetMember(member, $"位置 {position}");

            CharacterRuntimeData capturedMember = member;
            slotButton.onClick.AddListener(() => RemoveFromParty(partyManager, capturedMember));
            slotButton.interactable = !isBattleMode && party.Count > 1;
        }

        List<CharacterRuntimeData> sortedRoster = roster
            .Where(member => member != null && party.Contains(member))
            .OrderBy(member => member.Position)
            .Concat(roster.Where(member => member != null && !party.Contains(member)))
            .ToList();

        for (int i = 0; i < sortedRoster.Count; i++)
        {
            CharacterRuntimeData member = sortedRoster[i];
            if (member == null)
                continue;

            bool isInParty = party.Contains(member);
            PartyFormationEntryView entry = Instantiate(rosterEntryPrefab, rosterContent);
            entry.ConfigureInteraction(this);
            entry.SetMember(member, string.Empty, isInParty);

            Button entryButton = entry.EntryButton;
            entryButton.onClick.RemoveAllListeners();
            entryButton.onClick.AddListener(() => SelectRosterMember(partyManager, member));
            entryButton.interactable = !isBattleMode;

            EnsureEntryLayoutElement(entry.gameObject);
            spawnedRosterEntries.Add(entry.gameObject);
        }

        RectTransform rosterContentRect = rosterContent as RectTransform;
        if (rosterContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rosterContentRect);

        SetStatus(isBattleMode
            ? $"战斗中不可编队。当前队伍：{party.Count}/4 人"
            : $"当前队伍：{party.Count}/4 人    英雄名册：{roster.Count} 人");
    }

    /// <summary>
    /// 保证 Content 使用纵向布局，并按条目总高度自动扩展。
    /// 这样动态生成的条目不会堆叠在同一位置。
    /// </summary>
    private void EnsureRosterLayout()
    {
        RectTransform contentRect = rosterContent as RectTransform;
        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
        }

        VerticalLayoutGroup layout = rosterContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = rosterContent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rosterContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = rosterContent.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureEntryLayoutElement(GameObject entryObject)
    {
        LayoutElement layoutElement = entryObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = entryObject.AddComponent<LayoutElement>();

        RectTransform rectTransform = entryObject.transform as RectTransform;
        if (layoutElement.preferredHeight <= 0f)
        {
            float currentHeight = rectTransform != null ? rectTransform.rect.height : 0f;
            layoutElement.preferredHeight = currentHeight > 0f ? currentHeight : 80f;
        }

        layoutElement.flexibleHeight = 0f;
    }


    /// <summary>
    /// 创建独立的半透明 Left Idle 拖拽预览，不改变列表中原图的透明度。
    /// </summary>
    public void BeginCharacterDrag(PartyFormationEntryView sourceView, CharacterRuntimeData member, Vector2 screenPosition)
    {
        if (member == null || member.Definition == null || member.Definition.LeftIdle == null)
            return;

        bool isBattleMode = GameModeManager.Instance != null &&
                            GameModeManager.Instance.CurrentGameMode == GameMode.Battle;
        if (isBattleMode)
            return;

        draggedMember = member;

        if (dragCanvas == null)
        {
            dragCanvas = sourceView.GetComponentInParent<Canvas>();
            if (dragCanvas != null && dragCanvas.rootCanvas != null)
                dragCanvas = dragCanvas.rootCanvas;
        }

        if (dragCanvas == null)
        {
            Debug.LogWarning("无法开始角色拖拽：找不到父级 Canvas。", this);
            draggedMember = null;
            return;
        }

        if (dragPreviewRect == null)
        {
            GameObject previewObject = new GameObject("PartyDragPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewObject.transform.SetParent(dragCanvas.transform, false);
            dragPreviewRect = previewObject.GetComponent<RectTransform>();
            dragPreviewImage = previewObject.GetComponent<Image>();
            dragPreviewImage.raycastTarget = false;
            dragPreviewImage.preserveAspect = true;
        }

        dragPreviewRect.gameObject.SetActive(true);
        dragPreviewImage.sprite = member.Definition.LeftIdle;
        dragPreviewImage.color = new Color(1f, 1f, 1f, 0.58f);

        RectTransform sourceImageRect = sourceView.GetCharacterImageRect();
        Vector2 previewSize = sourceImageRect != null ? sourceImageRect.rect.size : new Vector2(96f, 96f);
        if (previewSize.x < 8f || previewSize.y < 8f)
            previewSize = new Vector2(96f, 96f);

        dragPreviewRect.sizeDelta = previewSize;
        dragPreviewRect.SetAsLastSibling();
        UpdateCharacterDrag(screenPosition);
    }

    public void UpdateCharacterDrag(Vector2 screenPosition)
    {
        if (dragPreviewRect == null || !dragPreviewRect.gameObject.activeSelf || dragCanvas == null)
            return;

        RectTransform canvasRect = dragCanvas.transform as RectTransform;
        Camera eventCamera = dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera;

        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            dragPreviewRect.anchoredPosition = localPoint;
        }
    }

    public void EndCharacterDrag(PartyFormationEntryView sourceView, Vector2 screenPosition)
    {
        if (draggedMember == null)
        {
            HideDragPreview();
            return;
        }

        CharacterRuntimeData member = draggedMember;
        HideDragPreview();

        bool isBattleMode = GameModeManager.Instance != null &&
                            GameModeManager.Instance.CurrentGameMode == GameMode.Battle;
        if (isBattleMode || EventSystem.current == null)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, hits);

        PartyFormationEntryView targetView = null;
        for (int i = 0; i < hits.Count; i++)
        {
            PartyFormationEntryView candidate = hits[i].gameObject.GetComponentInParent<PartyFormationEntryView>();
            if (candidate != null && candidate.ConfiguredSlotPosition > 0)
            {
                targetView = candidate;
                break;
            }
        }

        if (targetView == null)
            return;

        PartyManager partyManager = PartyManager.Instance;
        if (partyManager == null)
            return;

        int targetPosition = targetView.ConfiguredSlotPosition;
        bool alreadyInParty = partyManager.PartyMembers.Contains(member);

        if (alreadyInParty)
        {
            // 队伍内拖动使用交换，保留两名角色各自的槽位。
            partyManager.MoveMember(member, targetPosition);
            SetStatus($"已将 {member.DisplayName} 调整至 {targetPosition} 号位。");
        }
        else
        {
            CharacterRuntimeData occupant = partyManager.GetMemberAtPosition(targetPosition);
            if (partyManager.PartyMembers.Count >= 4 && occupant != null)
            {
                // 满员时拖入新角色会替换目标槽位成员，原成员仍保留在英雄名册。
                if (!partyManager.TryRemoveMemberFromParty(occupant))
                {
                    SetStatus("无法替换该位置的角色。");
                    return;
                }
            }

            if (!partyManager.TryAddMemberToParty(member))
            {
                SetStatus("无法入队：队伍可能已满。");
                RefreshView();
                return;
            }

            // 新入队角色按目标位置插入，其余队员依序补位。
            partyManager.MoveMemberToPosition(member, targetPosition);
            SetStatus($"已将 {member.DisplayName} 安排至 {targetPosition} 号位。");
        }

        RefreshView();
        ShowCharacterDetails(member);
    }

    private void HideDragPreview()
    {
        if (dragPreviewRect != null)
            dragPreviewRect.gameObject.SetActive(false);

        draggedMember = null;
    }

    private void SelectRosterMember(PartyManager partyManager, CharacterRuntimeData member)
    {
        ShowCharacterDetails(member);

        if (partyManager.PartyMembers.Contains(member))
        {
            SetStatus($"{member.DisplayName} 已在当前队伍中。");
            return;
        }

        bool success = partyManager.TryAddMemberToParty(member);
        SetStatus(success
            ? $"已将 {member.DisplayName} 加入队伍。"
            : "无法入队：队伍可能已满，或该角色不在英雄名册中。");

        RefreshView();
        ShowCharacterDetails(member);
    }

    private void RemoveFromParty(PartyManager partyManager, CharacterRuntimeData member)
    {
        bool success = partyManager.TryRemoveMemberFromParty(member);
        SetStatus(success
            ? $"已将 {member.DisplayName} 移出队伍，角色数据仍保留在名册中。"
            : "无法离队：队伍至少需要保留一名成员。");
        RefreshView();
    }

    private void ShowCharacterDetails(CharacterRuntimeData member)
    {
        if (characterDetailsText == null || member == null)
            return;

        StatBlock stats = member.GetTotalStats();
        StringBuilder details = new StringBuilder();
        details.AppendLine(member.DisplayName);
        details.AppendLine($"等级：{member.Level}");
        details.AppendLine($"HP：{member.CurrentHP}/{stats.MaxHP}");
        details.AppendLine($"SP：{member.CurrentSP}/{stats.MaxSP}");
        details.AppendLine($"BP：{member.CurrenBP}");
        details.AppendLine($"物理攻击：{stats.PAtk}");
        details.AppendLine($"魔法攻击：{stats.MAtk}");
        details.AppendLine($"物理防御：{stats.PDef}");
        details.AppendLine($"魔法防御：{stats.MDef}");
        details.AppendLine($"速度：{stats.Speed}");

        List<string> equipmentNames = new();
        if (member.EquipedItems != null)
        {
            foreach (CharacterRuntimeData.EquipedItemEntry itemEntry in member.EquipedItems)
            {
                if (itemEntry != null && itemEntry.item != null)
                    equipmentNames.Add($"{itemEntry.slot}：{itemEntry.item.name}");
            }
        }

        details.Append("装备：");
        details.Append(equipmentNames.Count > 0 ? string.Join("、", equipmentNames) : "无");
        characterDetailsText.text = details.ToString();
    }

    private void ClearPartySlots()
    {
        if (partySlots == null)
            return;

        for (int i = 0; i < partySlots.Length; i++)
        {
            PartyFormationEntryView slotView = partySlots[i];
            if (slotView == null)
                continue;

            slotView.SetEmpty($"位置 {i + 1}");
            slotView.EntryButton.onClick.RemoveAllListeners();
            slotView.EntryButton.interactable = false;
        }
    }

    private void ClearRosterEntries()
    {
        for (int i = 0; i < spawnedRosterEntries.Count; i++)
        {
            if (spawnedRosterEntries[i] != null)
                Destroy(spawnedRosterEntries[i]);
        }

        spawnedRosterEntries.Clear();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}

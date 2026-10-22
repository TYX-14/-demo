using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 编队面板控制器。
/// 队伍区域使用四个固定槽位，英雄名册使用可滚动列表。
/// UI 只读取角色运行时数据，并通过 PartyManager 修改队伍成员。
/// </summary>
public class PartyFormationPanelController : MonoBehaviour
{
    [Header("四个固定队伍槽位，按 1 至 4 号位排列")]
    [SerializeField] private Button[] partySlots = new Button[4];

    [Header("英雄名册滚动列表")]
    [SerializeField] private Transform rosterContent;

    [Tooltip("需要包含 Button 组件，并在子物体中放置一个 TMP_Text。")]
    [SerializeField] private Button rosterEntryButtonPrefab;

    [Header("英雄详情")]
    [SerializeField] private TMP_Text characterDetailsText;

    [Header("可选状态提示")]
    [SerializeField] private TMP_Text statusText;

    private readonly List<GameObject> spawnedRosterEntries = new();

    private void OnEnable()
    {
        RefreshView();
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
            rosterContent == null || rosterEntryButtonPrefab == null)
        {
            SetStatus("请绑定 4 个队伍槽位、英雄名册内容区域和名册条目预制体。");
            return;
        }

        List<CharacterRuntimeData> party = partyManager.PartyMembers;
        IReadOnlyList<CharacterRuntimeData> roster = rosterManager.OwnedCharacters;
        bool isBattleMode = GameModeManager.Instance != null &&
                            GameModeManager.Instance.CurrentGameMode == GameMode.Battle;

        for (int i = 0; i < partySlots.Length; i++)
        {
            Button slotButton = partySlots[i];
            if (slotButton == null)
                continue;

            int position = i + 1;
            CharacterRuntimeData member = partyManager.GetMemberAtPosition(position);
            TMP_Text slotText = slotButton.GetComponentInChildren<TMP_Text>(true);

            slotButton.onClick.RemoveAllListeners();

            if (member == null)
            {
                if (slotText != null)
                    slotText.text = $"位置 {position}\n空槽";

                slotButton.interactable = false;
                continue;
            }

            if (slotText != null)
            {
                StatBlock stats = member.GetTotalStats();
                slotText.text =
                    $"位置 {position}\n{member.DisplayName}  Lv.{member.Level}\n" +
                    $"HP {member.CurrentHP}/{stats.MaxHP}  SP {member.CurrentSP}/{stats.MaxSP}";
            }

            CharacterRuntimeData capturedMember = member;
            slotButton.onClick.AddListener(() => RemoveFromParty(partyManager, capturedMember));
            slotButton.interactable = !isBattleMode && party.Count > 1;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterRuntimeData member = roster[i];
            if (member == null)
                continue;

            bool isInParty = party.Contains(member);
            StatBlock stats = member.GetTotalStats();
            string label = isInParty
                ? $"[在队] {member.DisplayName}  Lv.{member.Level}\nHP {member.CurrentHP}/{stats.MaxHP}"
                : $"{member.DisplayName}  Lv.{member.Level}\nHP {member.CurrentHP}/{stats.MaxHP}";

            Button entry = CreateRosterEntry(label, () => SelectRosterMember(partyManager, member));
            entry.interactable = !isBattleMode;
        }

        SetStatus(isBattleMode
            ? $"战斗中不可编队。当前队伍：{party.Count}/4 人"
            : $"当前队伍：{party.Count}/4 人    英雄名册：{roster.Count} 人");
    }

    private Button CreateRosterEntry(string label, UnityEngine.Events.UnityAction onClick)
    {
        Button entry = Instantiate(rosterEntryButtonPrefab, rosterContent);
        TMP_Text text = entry.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
            text.text = label;

        entry.onClick.RemoveAllListeners();
        entry.onClick.AddListener(onClick);
        spawnedRosterEntries.Add(entry.gameObject);
        return entry;
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
            if (partySlots[i] == null)
                continue;

            TMP_Text slotText = partySlots[i].GetComponentInChildren<TMP_Text>(true);
            if (slotText != null)
                slotText.text = $"位置 {i + 1}\n空槽";

            partySlots[i].onClick.RemoveAllListeners();
            partySlots[i].interactable = false;
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

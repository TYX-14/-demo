using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 编队面板的第一阶段控制器。
/// 负责展示当前队伍与英雄名册，并调用 PartyManager 的入队/离队接口。
/// UI 不持有或创建角色运行时数据。
/// </summary>
public class PartyFormationPanelController : MonoBehaviour
{
    [Header("当前队伍")]
    [SerializeField] private Transform partyContent;

    [Header("英雄名册")]
    [SerializeField] private Transform rosterContent;

    [Header("通用条目")]
    [Tooltip("需要包含 Button 组件，并在子物体中放置一个 TMP_Text。")]
    [SerializeField] private Button entryButtonPrefab;

    [Header("可选状态提示")]
    [SerializeField] private TMP_Text statusText;

    private readonly List<GameObject> spawnedEntries = new();

    private void OnEnable()
    {
        RefreshView();
    }

    /// <summary>
    /// 重新读取管理器中的数据并刷新两列显示。
    /// 当入队、离队或角色状态改变后可再次调用。
    /// </summary>
    public void RefreshView()
    {
        ClearEntries();

        PartyManager partyManager = PartyManager.Instance;
        CharacterRosterManager rosterManager = CharacterRosterManager.Instance;

        if (partyManager == null || rosterManager == null)
        {
            SetStatus("无法读取队伍或英雄名册，请检查场景中的管理器。");
            return;
        }

        if (partyContent == null || rosterContent == null || entryButtonPrefab == null)
        {
            SetStatus("编队面板尚未绑定队伍列表、名册列表或条目预制体。");
            return;
        }

        IReadOnlyList<CharacterRuntimeData> roster = rosterManager.OwnedCharacters;
        List<CharacterRuntimeData> party = partyManager.PartyMembers;
        bool isBattleMode = GameModeManager.Instance != null &&
                            GameModeManager.Instance.CurrentGameMode == GameMode.Battle;

        for (int i = 0; i < party.Count; i++)
        {
            CharacterRuntimeData member = party[i];
            if (member == null)
                continue;

            Button entry = CreateEntry(
                partyContent,
                $"[{member.Position}号位] {member.DisplayName}  Lv.{member.Level}  HP {member.CurrentHP}/{member.GetTotalStats().MaxHP}",
                () => RemoveFromParty(partyManager, member));
            entry.interactable = !isBattleMode;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterRuntimeData member = roster[i];
            if (member == null)
                continue;

            bool isInParty = party.Contains(member);
            string label = isInParty
                ? $"[在队] {member.DisplayName}  Lv.{member.Level}  HP {member.CurrentHP}/{member.GetTotalStats().MaxHP}"
                : $"{member.DisplayName}  Lv.{member.Level}  HP {member.CurrentHP}/{member.GetTotalStats().MaxHP}";

            Button entry = CreateEntry(rosterContent, label, () => AddToParty(partyManager, member));
            entry.interactable = !isBattleMode && !isInParty && party.Count < 4;
        }

        SetStatus(isBattleMode
            ? $"战斗中不可编队。当前队伍：{party.Count}/4 人"
            : $"当前队伍：{party.Count}/4 人    英雄名册：{roster.Count} 人");
    }

    private Button CreateEntry(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button entry = Instantiate(entryButtonPrefab, parent);
        TMP_Text text = entry.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
            text.text = label;

        entry.onClick.RemoveAllListeners();
        entry.onClick.AddListener(onClick);
        spawnedEntries.Add(entry.gameObject);
        return entry;
    }

    private void AddToParty(PartyManager partyManager, CharacterRuntimeData member)
    {
        bool success = partyManager.TryAddMemberToParty(member);
        SetStatus(success ? $"已将 {member.DisplayName} 加入队伍。" : "无法入队：请检查队伍人数及英雄名册。");
        RefreshView();
    }

    private void RemoveFromParty(PartyManager partyManager, CharacterRuntimeData member)
    {
        bool success = partyManager.TryRemoveMemberFromParty(member);
        SetStatus(success ? $"已将 {member.DisplayName} 移出队伍，角色数据仍保留在名册中。" : "无法离队：队伍至少需要保留一名成员。");
        RefreshView();
    }

    private void ClearEntries()
    {
        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i]);
        }

        spawnedEntries.Clear();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}

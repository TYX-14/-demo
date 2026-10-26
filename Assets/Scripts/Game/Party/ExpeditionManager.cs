using System.Collections.Generic;
using UnityEngine;

/// <summary>最小远征生命周期管理器。远征出发与返城流程应显式调用 BeginExpedition / EndExpedition。</summary>
public class ExpeditionManager : singleton<ExpeditionManager>
{
    [Header("精神状态规则")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("基础决心考验美德概率。其他效果可通过角色的 VirtueChanceBonus 增减最终概率。")]
    private float baseVirtueChance = 0.25f;

    public bool IsExpeditionActive { get; private set; }

    public static float GetVirtueChance(CharacterRuntimeData character)
    {
        float baseChance = Instance != null ? Instance.baseVirtueChance : 0.25f;
        float bonus = character != null ? character.VirtueChanceBonus : 0f;
        return Mathf.Clamp01(baseChance + bonus);
    }

    /// <summary>开始一次新的远征。只重置本次远征的美德/折磨标记，不清空角色压力。</summary>
    public bool BeginExpedition()
    {
        if (IsExpeditionActive)
        {
            Debug.LogWarning("远征已经开始，忽略重复的 BeginExpedition 调用。", this);
            return false;
        }

        IsExpeditionActive = true;
        foreach (CharacterRuntimeData character in GetOwnedCharacters())
        {
            if (character != null)
                character.ResetExpeditionSpiritState();
        }
        return true;
    }

    /// <summary>远征结束时结算：美德者压力归零，折磨者压力设为 100，其他角色保留当前压力。</summary>
    public bool EndExpedition()
    {
        if (!IsExpeditionActive)
        {
            Debug.LogWarning("当前没有进行中的远征，忽略 EndExpedition 调用。", this);
            return false;
        }

        foreach (CharacterRuntimeData character in GetOwnedCharacters())
        {
            if (character == null)
                continue;

            switch (character.SpiritState)
            {
                case CharacterSpiritState.Virtue:
                    character.CurrentStress = 0;
                    break;
                case CharacterSpiritState.Affliction:
                    character.CurrentStress = 100;
                    break;
                case CharacterSpiritState.Normal:
                    break;
            }

            character.ResetExpeditionSpiritState();
            CharacterStressSystem.NotifyCharacterUpdated(character);
        }

        IsExpeditionActive = false;
        return true;
    }

    private IEnumerable<CharacterRuntimeData> GetOwnedCharacters()
    {
        var seen = new HashSet<CharacterRuntimeData>();

        if (CharacterRosterManager.Instance != null)
        {
            foreach (CharacterRuntimeData character in CharacterRosterManager.Instance.OwnedCharacters)
            {
                if (character != null && seen.Add(character))
                    yield return character;
            }
        }

        if (PartyManager.Instance != null)
        {
            foreach (CharacterRuntimeData character in PartyManager.Instance.PartyMembers)
            {
                if (character != null && seen.Add(character))
                    yield return character;
            }
        }
    }
}

using System;

/// <summary>
/// 角色精神状态的轻量通知中心。
/// 运行时数据负责保存状态；UI 只订阅通知，不反过来决定战斗规则。
/// </summary>
public enum CharacterSpiritState
{
    Normal,
    Virtue,
    Affliction
}

public static class CharacterStressSystem
{
    public static event Action<CharacterRuntimeData> CharacterUpdated;

    public static void NotifyCharacterUpdated(CharacterRuntimeData character)
    {
        if (character != null)
            CharacterUpdated?.Invoke(character);
    }
}

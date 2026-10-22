using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 英雄名册：统一持有玩家拥有的角色运行时数据。
/// 当前阶段只建立基础管理能力，暂不接管现有招募流程。
/// </summary>
public class CharacterRosterManager : singleton<CharacterRosterManager>
{
    [Header("已拥有的英雄")]
    [SerializeField]
    private List<CharacterRuntimeData> ownedCharacters = new();

    /// <summary>
    /// 获取名册中的全部英雄数据。
    /// 外部可以读取，但不能通过此属性替换名册列表。
    /// </summary>
    public IReadOnlyList<CharacterRuntimeData> OwnedCharacters => ownedCharacters;

    /// <summary>
    /// 判断名册是否已经持有指定角色实例。
    /// </summary>
    public bool ContainsCharacter(CharacterRuntimeData character)
    {
        return character != null && ownedCharacters.Contains(character);
    }

    /// <summary>
    /// 将已有的角色实例加入名册。
    /// 不创建角色数据，也不重新初始化装备。
    /// </summary>
    public bool AddCharacter(CharacterRuntimeData character)
    {
        if (character == null || ownedCharacters.Contains(character))
            return false;

        ownedCharacters.Add(character);
        return true;
    }

    /// <summary>
    /// 从名册移除指定角色实例。
    /// 仅应在永久移除角色时调用，普通离队不应调用此方法。
    /// </summary>
    public bool RemoveCharacter(CharacterRuntimeData character)
    {
        if (character == null)
            return false;

        return ownedCharacters.Remove(character);
    }
}

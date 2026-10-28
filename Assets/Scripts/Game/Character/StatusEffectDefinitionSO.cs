using UnityEngine;

/// <summary>可复用状态的类别。具体触发逻辑由后续状态系统统一处理。</summary>
public enum StatusEffectType
{
    Stun,
    DamageOverTime,
    Counterattack,
    Counterspell,
    StatModifier,
    Custom
}

/// <summary>
/// 状态效果的静态定义。这里不保存角色当前剩余回合数、层数或次数。
/// 运行时状态数据将在后续状态管理器中单独保存。
/// </summary>
[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Battle/Status Effect Definition")]
public class StatusEffectDefinitionSO : ScriptableObject
{
    [SerializeField] private string statusName;
    [TextArea(2, 5)] [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private StatusEffectType effectType;

    public string StatusName => statusName;
    public string Description => description;
    public Sprite Icon => icon;
    public StatusEffectType EffectType => effectType;
}

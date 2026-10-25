using UnityEngine;

[CreateAssetMenu(menuName = "Character/Enemy")]
public class EnemyDfinitionSo : CharacterDefinitionSO
{
    [Header("敌方逻辑站位")]
    [Range(1, 4)]
    [Tooltip("敌人在遭遇中的默认逻辑起始位置。多个敌人可以配置相同位置。")]
    public int defaultBattlePosition = 1;

    [Range(1, 4)]
    [Tooltip("该敌人占据的连续逻辑位置数量。普通敌人通常为 1，大型敌人可以为 2 到 4。")]
    public int occupiedSlotCount = 1;

    private void OnValidate()
    {
        occupiedSlotCount = Mathf.Clamp(occupiedSlotCount, 1, 4);
        defaultBattlePosition = Mathf.Clamp(defaultBattlePosition, 1, 5 - occupiedSlotCount);
    }
}

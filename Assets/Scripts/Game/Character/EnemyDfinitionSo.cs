using UnityEngine;

[CreateAssetMenu(menuName = "Character/Enemy")]
public class EnemyDfinitionSo : CharacterDefinitionSO
{
    [Header("敌方体型")]
    [Range(1, 4)]
    [Tooltip("该敌人占据的连续逻辑位置数量。普通敌人通常为 1，大型敌人可以为 2 到 4。")]
    public int occupiedSlotCount = 1;

    [Header("期望站位范围")]
    [Range(1, 4)]
    [Tooltip("敌人期望占据的最低逻辑位置编号。1、2 为前排，3、4 为后排。")]
    public int preferredPositionMin = 1;

    [Range(1, 4)]
    [Tooltip("敌人期望占据的最高逻辑位置编号。该范围是站位偏好，生成器会优先满足，但不会因此产生占格冲突。")]
    public int preferredPositionMax = 4;

    private void OnValidate()
    {
        occupiedSlotCount = Mathf.Clamp(occupiedSlotCount, 1, 4);
        preferredPositionMin = Mathf.Clamp(preferredPositionMin, 1, 4);
        preferredPositionMax = Mathf.Clamp(preferredPositionMax, preferredPositionMin, 4);
    }
}

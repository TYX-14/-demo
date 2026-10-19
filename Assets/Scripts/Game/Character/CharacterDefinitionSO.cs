/// <summary>
/// 角色定义 ScriptableObject。
/// 用于存储角色的基础属性、动画机等信息。
/// </summary>
public abstract class CharacterDefinitionSO : ScriptableObject
{
    [Header("角色初始化")]
    public string ID;
    public string Name;
    public Sprite Portrait;
    public Sprite LeftIdle;

    [Header("属性")]
    public int BaseLevel = 1;
    public StatBlock BaseStats;

    [Header("战斗机动性")]
    [Min(0)]
    [Tooltip("主动战斗换位时，角色一次最多跨越的阵型格数。")]
    public int maxSwapDistance = 1;

    [Header("动画机")]
    public AnimatorOverrideController filedAnimator;
    public AnimatorOverrideController battleAnimator;
}


[System.Serializable]
public struct StatBlock
{
    public int MaxHP;
    public int MaxSP;
    public int PAtk;
    public int PDef;
    public int MAtk;
    public int MDef;
    public int Speed;
    public int Accuracy;
    public int Evasion;

    public static StatBlock Zero = new();

    public static StatBlock operator+(StatBlock a, StatBlock b)
    {
        return new() 
        {
            MaxHP = a.MaxHP+b.MaxHP,
            MaxSP = a.MaxSP+b.MaxSP,
            PAtk = a.PAtk+b.PAtk,
            PDef = a.PDef+b.PDef,
            MAtk = a.MAtk+b.MAtk,
            MDef = a.MDef+b.MDef,
            Speed = a.Speed+b.Speed,
            Accuracy = a.Accuracy+b.Accuracy,
            Evasion = a.Evasion+b.Evasion,
        };


    }

    // 重载乘法运算符：允许 StatBlock 直接与 float 相乘 (例如：statBlock * -1f)
    public static StatBlock operator *(StatBlock a, float multiplier)
    {
        // 实例化并返回一个新的 StatBlock 对象，每个属性字段都乘以系数并四舍五入取整
        return new StatBlock
        {
            // Mathf.RoundToInt 会将 float 结果四舍五入为最接近的整数 (int)
            MaxHP = Mathf.RoundToInt(a.MaxHP * multiplier),    // 最大生命值
            MaxSP = Mathf.RoundToInt(a.MaxSP * multiplier),    // 最大技能值/魔法值
            PAtk = Mathf.RoundToInt(a.PAtk * multiplier),     // 物理攻击力
            PDef = Mathf.RoundToInt(a.PDef * multiplier),     // 物理防御力
            MAtk = Mathf.RoundToInt(a.MAtk * multiplier),     // 魔法攻击力
            MDef = Mathf.RoundToInt(a.MDef * multiplier),     // 魔法防御力
            Speed = Mathf.RoundToInt(a.Speed * multiplier),    // 速度值
            Accuracy = Mathf.RoundToInt(a.Accuracy * multiplier), // 命中率/精准度
            Evasion = Mathf.RoundToInt(a.Evasion * multiplier)   // 闪避率
        };
    }
}
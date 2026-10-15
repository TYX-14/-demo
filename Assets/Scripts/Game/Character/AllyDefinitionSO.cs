
[CreateAssetMenu(menuName ="Character/Ally")]
/// <summary>
/// 友军定义 ScriptableObject。    
public class AllyDefinitionSO : CharacterDefinitionSO
{
    [Header("友军职业")]
    public PlayerJob job;

    [Header("成长设置")]
    public GobalGrowthConfigSO globalGrowthConfigSO;
    public GrowProfile growProfile;

    [Header("角色可用装备列表")]
    public List<WeaqonType> EquipableweaqonTypes = new();

    [Header("初始化装备")]
    public List<InitialEquipmentEntry> initialEquipments = new(); 


    [System.Serializable]
    public struct InitialEquipmentEntry
    {
        public EquipSlot slot;
        public EquipmentItemSO item;
    }


    public bool CanEquipWeaponType(WeaqonType weaqonType)
    {
        if(weaqonType == WeaqonType.None) return false;
        return EquipableweaqonTypes.Contains(weaqonType);
    }

    #region 属性成长方法
    /// <summary>
    /// 计算等级成长加值
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public StatBlock GetStatForLevel(int level)
    {
        float hp = globalGrowthConfigSO.GetCurveByRank(growProfile.Hp).Evaluate(level);
        float sp = globalGrowthConfigSO.GetCurveByRank(growProfile.SP).Evaluate(level);
        float pAtk = globalGrowthConfigSO.GetCurveByRank(growProfile.PAtk).Evaluate(level);
        float mAtk = globalGrowthConfigSO.GetCurveByRank((growProfile.MAtk)).Evaluate(level);
        float mDef = globalGrowthConfigSO.GetCurveByRank(growProfile.MDef).Evaluate(level);
        float pDef = globalGrowthConfigSO.GetCurveByRank(growProfile.PDef).Evaluate(level);
        float speed = globalGrowthConfigSO.GetCurveByRank(growProfile.speed).Evaluate(level);

        return new StatBlock()
        {
            MaxHP = Mathf.RoundToInt(BaseStats.MaxHP * hp),//Mathf.RoundToInt(...):四舍五入为int
            MaxSP = Mathf.RoundToInt(BaseStats.MaxSP * sp),
            PAtk = Mathf.RoundToInt(BaseStats.PAtk * pAtk),
            PDef = Mathf.RoundToInt(BaseStats.PDef * pDef),
            MAtk = Mathf.RoundToInt(BaseStats.MAtk * mAtk),
            MDef = Mathf.RoundToInt(BaseStats.MDef * mDef),
            Speed = Mathf.RoundToInt(BaseStats.Speed * speed),
            Accuracy = BaseStats.Accuracy,
            Evasion = BaseStats.Evasion,
        };
    }
    #endregion
}

[System.Serializable]

public struct GrowProfile
{
    public GrowthRank Hp;
    public GrowthRank SP;
    public GrowthRank PAtk;
    public GrowthRank PDef;
    public GrowthRank MAtk;
    public GrowthRank MDef;
    public GrowthRank speed;
    
}
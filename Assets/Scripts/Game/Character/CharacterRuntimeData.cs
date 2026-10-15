using System;
using System.ComponentModel;
[Serializable]
public class CharacterRuntimeData
{
    public CharacterDefinitionSO Definition;
    public int Level;
    public int CurrentHP;
    public int CurrentSP;
    public int CurrenBP;
    public int CurrenEXP;
    public int Position;

    public string DisplayName => Definition.Name; 

    public StatBlock EquipmentStats;//装备属性


    [Serializable]
    public class EquipedItemEntry
    {
        public EquipSlot slot;
        public EquipmentItemSO item;
    }

    public List<EquipedItemEntry> EquipedItems = new();

    public  CharacterRuntimeData(CharacterDefinitionSO definition)
    { 
        Definition = definition; 
        EquipmentStats = StatBlock.Zero;
        Level = definition.BaseLevel;

        var Stats =  GetTotalStats();
        CurrentHP = Stats.MaxHP;
        CurrentSP = Stats.MaxSP;
        CurrenBP = 0;

        ApplyIntialEquipment();
    }
    /// <summary>
    /// 拿到友军或者敌人的基本属性（计算后）
    /// </summary>
    /// <returns></returns>
    public StatBlock GetBaseStats()
    {
        if (Definition is AllyDefinitionSO allyDefinition)
        {
            return allyDefinition.GetStatForLevel(Level);
        }
        if (Definition is EnemyDfinitionSo enemyDfinitio)
        {
            return enemyDfinitio.BaseStats;
        }
        return Definition != null? Definition.BaseStats : StatBlock.Zero;
    }
    /// <summary>
    /// 得到总属性：基础+装备
    /// </summary>
    /// <returns></returns>
    public StatBlock GetTotalStats() => GetBaseStats()+EquipmentStats;

    #region 属性变化方法
    public void HealFull()
    {
        CurrentHP = GetTotalStats().MaxHP;
        CurrentSP = GetTotalStats().MaxSP;

    }

    /// <summary>
    /// hp改变
    /// </summary>
    /// <param name="amount"></param>
    public void ModifyHp(int amount)
    {
        CurrentHP += amount;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, GetTotalStats().MaxHP);
    }
    /// <summary>
    /// Mp改变
    /// </summary>
    /// <param name="amount"></param>
    public void ModifyMp(int amount)
    {
        CurrentSP += amount;
        CurrentSP = Mathf.Clamp(CurrentSP, 0, GetTotalStats().MaxSP);
    }
    public void ResetBattleBP()
    {
        CurrenBP = 0;   
    }
    #endregion


    #region 挑战战力评估

    /// <summary>
    /// 根据角色的基础属性块评估并计算其战力评分
    /// </summary>
    /// <param name="stats">角色的属性数据块</param>
    /// <returns>计算后的整型战力值（最低为 1）</returns>
    public static int EvaluatePowerFromStats(StatBlock stats)
    {
        // 根据各项属性权重加权求和
        float score =
            stats.MaxHP * 0.2f +
            stats.MaxSP * 0.1f +
            stats.PAtk * 1.5f +
            stats.MAtk * 1.5f +
            stats.PDef * 1.0f +
            stats.MDef * 1.0f +
            stats.Speed * 1.2f;

        // 四舍五入为整数，并确保最终战力评估值不低于 1
        return Mathf.Max(1, Mathf.RoundToInt(score));
    }

    #endregion
    #region 装备系统
    private void ApplyIntialEquipment()
    {
        
        AllyDefinitionSO allDef = Definition as AllyDefinitionSO;

        if (allDef == null || allDef.initialEquipments == null||allDef.initialEquipments.Count==0)
        {
            
            return;
        }


        for (int i = 0; i < allDef.initialEquipments.Count; i++)
        {
            var entry = allDef.initialEquipments[i];
            var item = entry.item;
            if(item==null) continue;

            if (InventoryMananger.Instance != null)
            {
                SetEquippedItem(entry.slot,item);

                InventoryMananger.Instance.AddItem(item, 1);
            }
            
            
        }
        
        
    }


    #endregion


    #region 工具：增删改查

    public void SetEquippedItem(EquipSlot slot, EquipmentItemSO item)
    {
        var entry = EquipedItems.Find(e=>e.slot == slot);
        //如果装备不是空的
        if (entry != null)
        {
            if (item != null)
            {
                EquipedItems.Remove(entry);
            }
            entry.item = item;
        }
        else
        {
            EquipedItems.Add(new EquipedItemEntry() { slot=slot,item = item});
        }
        RebuildEquipmentStats();
    }


    public EquipmentItemSO GetEquippedItem(EquipSlot slot)
    {
        var entry = EquipedItems.Find(entry => entry != null && entry.slot == slot);

        return entry?.item;

    }

    public int GetEquippedItemCount(ItemDefinitionSO targetItem)
    {
        if(targetItem == null) return 0;

        int count = 0;

        foreach (var entry in EquipedItems)
        {
            if(entry!=null&&entry.item==targetItem)
                count++;
        }
        return count;
    }
    /// <summary>
    /// 刷新装备加成属性
    /// </summary>
    public void RebuildEquipmentStats()
    {
        var merged = StatBlock.Zero;
        for (int i = EquipedItems.Count - 1; i >= 0; i--)
        {
            var entry = EquipedItems[i];
            if (entry == null || entry.item == null)
            {
                EquipedItems.Remove(entry);
                continue;
            }
            merged += entry.item.statBonus;
        }

        EquipmentStats = merged;

        var total = GetTotalStats();
        CurrentHP = Math.Min(CurrentHP, total.MaxHP);
        CurrentSP = Math.Min(CurrentSP, total.MaxSP);
    }


    #endregion
}
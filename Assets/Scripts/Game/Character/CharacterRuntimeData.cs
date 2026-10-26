using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
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

    [Header("远征精神状态")]
    [Range(0, 200)] public int CurrentStress;
    public CharacterSpiritState SpiritState = CharacterSpiritState.Normal;
    [Min(0f)] public float VirtueChanceBonus;
    [Range(-1f, 1f)] public float DeathblowResistanceBonus;
    public bool IsAtDeathsDoor;
    public bool IsDead;

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

        // Position 由队伍管理器（友军）或 EnemySpawnManager（敌军）负责分配。
        // 构造函数不再从敌人定义中决定本场战斗的实际位置。

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
        if (IsDead)
            return;

        CurrentHP = GetTotalStats().MaxHP;
        CurrentSP = GetTotalStats().MaxSP;
        IsAtDeathsDoor = false;
        CharacterStressSystem.NotifyCharacterUpdated(this);
    }

    /// <summary>
    /// hp改变
    /// </summary>
    /// <param name="amount"></param>
    public void ModifyHp(int amount)
    {
        if (IsDead || amount == 0)
            return;

        if (amount > 0)
        {
            CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, GetTotalStats().MaxHP);
            if (CurrentHP > 0)
                IsAtDeathsDoor = false;
            CharacterStressSystem.NotifyCharacterUpdated(this);
            return;
        }

        // 角色已处于死门且生命仍为 0：下一次伤害交由致死判定处理。
        if (CurrentHP <= 0 && IsAtDeathsDoor)
        {
            ResolveDeathblowCheck();
            return;
        }

        CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, GetTotalStats().MaxHP);
        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            IsAtDeathsDoor = true;
        }

        CharacterStressSystem.NotifyCharacterUpdated(this);
    }

    /// <summary>
    /// 修改压力。美德期间压力完全锁定；折磨状态一旦触发，会持续到远征结束。
    /// </summary>
    public void ModifyStress(int amount)
    {
        if (amount == 0 || SpiritState == CharacterSpiritState.Virtue || IsDead)
            return;

        CurrentStress = Mathf.Clamp(CurrentStress + amount, 0, 200);

        if (amount > 0 && SpiritState == CharacterSpiritState.Normal && CurrentStress >= 100)
        {
            // 决心考验固定在 100 点发生。若一次增加跨过阈值，美德不会被溢出的压力推高。
            int overflowStress = Mathf.Max(0, CurrentStress - 100);
            CurrentStress = 100;

            float chance = ExpeditionManager.GetVirtueChance(this);
            SpiritState = UnityEngine.Random.value < chance
                ? CharacterSpiritState.Virtue
                : CharacterSpiritState.Affliction;

            if (SpiritState == CharacterSpiritState.Affliction)
                CurrentStress = Mathf.Min(200, 100 + overflowStress);
        }

        if (SpiritState == CharacterSpiritState.Affliction && CurrentStress >= 200)
        {
            // 心力衰竭本身不直接判死，只造成一次无法闪避、无法减免的特殊伤害。
            CurrentStress = 180;
            ApplyHeartAttack();
        }

        CharacterStressSystem.NotifyCharacterUpdated(this);
    }

    /// <summary>
    /// 心力衰竭：若仍有生命，直接将生命归零并进入死门；
    /// 若已在死门且生命仍为零，则进行致死判定。
    /// </summary>
    private void ApplyHeartAttack()
    {
        if (IsDead)
            return;

        if (CurrentHP <= 0 && IsAtDeathsDoor)
        {
            ResolveDeathblowCheck();
            return;
        }

        CurrentHP = 0;
        IsAtDeathsDoor = true;
    }

    private void ResolveDeathblowCheck()
    {
        if (IsDead)
            return;

        float resistance = Definition != null
            ? Definition.BaseDeathblowResistance + DeathblowResistanceBonus
            : 0.67f;
        resistance = Mathf.Clamp01(resistance);

        if (UnityEngine.Random.value >= resistance)
        {
            IsDead = true;
            CurrentHP = 0;
            IsAtDeathsDoor = false;
        }

        CharacterStressSystem.NotifyCharacterUpdated(this);
    }

    /// <summary>
    /// 开始新远征时清除本次远征的美德/折磨标记，不重置压力。
    /// 压力结算由 ExpeditionManager.EndExpedition 统一处理。
    /// </summary>
    public void ResetExpeditionSpiritState()
    {
        SpiritState = CharacterSpiritState.Normal;
        CharacterStressSystem.NotifyCharacterUpdated(this);
    }
    /// <summary>
    /// Mp改变
    /// </summary>
    /// <param name="amount"></param>
    public void ModifyMp(int amount)
    {
        if (IsDead || amount == 0)
            return;

        CurrentSP += amount;
        CurrentSP = Mathf.Clamp(CurrentSP, 0, GetTotalStats().MaxSP);
        CharacterStressSystem.NotifyCharacterUpdated(this);
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
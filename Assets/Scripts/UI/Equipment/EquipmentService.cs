
public static class EquipmentService
{
    /// <summary>
    /// 根据目标装备槽位，从背包中筛选出所有可装备的候选物品列表
    /// </summary>
    /// <param name="inventory">背包管理器实例</param>
    /// <param name="targetSlot">目标装备槽位（如：头部、身体、武器等）</param>
    /// <returns>符合条件的装备 ScriptableObject 列表</returns>
    public static List<EquipmentItemSO> BuildCandidates(InventoryMananger inventory, EquipSlot targetSlot)
    {
        // 初始化一个存储筛选结果的列表
        List<EquipmentItemSO> result = new() { null};

        // 遍历背包中的每一个物品槽位
        for (int i = 0; i < inventory.CurrentInventory.Count; i++)
        {
            var item = inventory.CurrentInventory[i];

            // 1. 安全检查：如果物品为空，或者物品数量小于等于 0，则跳过
            if (item == null || item.Quantity <= 0)
                continue;

            // 2. 类型检查与转换：检查该物品的定义是否属于“装备”(EquipmentItemSO)
            // 如果不是装备，则跳过；如果是，则将其模式匹配并重命名为 equipmentItem
            if (item.ItemDefinition is not EquipmentItemSO equipmentItem)
                continue;

            // 3. 兼容性检查：检查该装备是否能放入当前的目标槽位
           
            if (!IsSlotCompatible(equipmentItem, targetSlot))
                continue;

            // 4. 完美符合条件，将装备加入到候选列表中
            result.Add(equipmentItem);
        }

        // 返回最终筛选出的所有可用装备
        return result;
    }

    /// <summary>
    /// 检查指定的装备是否与目标装备槽位兼容（匹配）
    /// </summary>
    /// <param name="item">要检查的装备数据</param>
    /// <param name="targetSlot">目标装备槽位</param>
    /// <returns>如果匹配返回 true，否则返回 false</returns>
    private static bool IsSlotCompatible(EquipmentItemSO item, EquipSlot targetSlot)
    {
        // 防御性代码：如果传入的装备为空，直接返回不匹配
        if (item == null)
            return false;

        // 使用 C# 的 switch 表达式，根据装备的类型(category)进行分支判断
        return item.category switch
        {
            // 武器类：通过将武器子类型(weaponType)的枚举整型值减 1，来匹配对应的武器槽位
            EquipmentCategory.Weapon => targetSlot == (EquipSlot)((int)item.weaponType - 1),

            // 盾牌类：必须对应盾牌槽位
            EquipmentCategory.Shield => targetSlot == EquipSlot.Shield,

            // 头部装备：必须对应头部槽位
            EquipmentCategory.Head => targetSlot == EquipSlot.Head,

            // 身体装备：必须对应身体槽位
            EquipmentCategory.Body => targetSlot == EquipSlot.Body,

            // 饰品类：可以放入“饰品1”槽位 或者 “饰品2”槽位
            EquipmentCategory.Accessory => targetSlot == EquipSlot.Accessory1 || targetSlot == EquipSlot.Accessory2,

            // 默认情况：如果是未知的装备类型，一律返回不匹配
            _ => false,
        };
    }

    /// <summary>
    /// 获取物品的可用数量（总持有数量 - 已装备数量）
    /// 如果是非装备物品，直接返回总持有量。
    /// </summary>
    public static int GetAvailableItemCount(InventoryMananger inventory, PartyManager partyManager, ItemDefinitionSO item)
    {
        // 非空检查
        if (item == null)
            return 0;

        // 获取背包中的总持有量
        int totalQuantity = inventory.GetItemQuantity(item);
        if (totalQuantity <= 0)
            return 0;

        int equippedCount = 0;
        // 遍历所有队伍成员，累加该物品已被装备的数量
        for (int i = 0; i < partyManager.PartyMembers.Count; i++)
        {
            equippedCount += partyManager.PartyMembers[i].GetEquippedItemCount(item);
        }

        // 可用数量 = 总持有量 - 已装备总量
        return totalQuantity - equippedCount;
    }
    /// <summary>
    /// 计算并构建角色试穿/更换装备后的预览总属性。
    /// <para>逻辑：当前总属性 - 当前槽位已装备属性 + 新预览物品属性</para>
    /// </summary>
    /// <param name="member">需要预览属性的目标角色运行时数据。</param>
    /// <param name="slot">当前操作的装备槽位（如：武器、防具等）。</param>
    /// <param name="previewItem">想要试穿/预览的新装备物品数据；如果为 null，则代表脱下该槽位的装备。</param>
    /// <returns>返回计算完毕后的最终预览属性块（StatBlock）。如果角色数据为空，则返回零属性块。</returns>
    public static StatBlock BuildPreviewTotalStats(CharacterRuntimeData member, EquipSlot slot, EquipmentItemSO previewItem)
    {
        // 1. 安全检查：如果传入的角色数据为空，直接返回一个属性全为 0 的空属性块
        if (member == null)
        {
            return StatBlock.Zero;
        }

        // 2. 获取该角色当前的最终总属性（包含当前已穿戴的所有装备加成）
        StatBlock previewTotal = member.GetTotalStats(); // 获取当前角色的总属性

        // 3. 获取目标装备槽位（如：武器、防具等）当前正穿戴的装备
        EquipmentItemSO currentItem = member.GetEquippedItem(slot); // 获取当前装备的物品

        // 4. 如果当前槽位脱衣服/换衣服，需要先扣除原有装备的属性
        if (currentItem != null)
        {
            // 这里通过乘以 -1f，利用重载的乘法运算符将所有属性取反，达到“减去”的效果
            previewTotal += currentItem.statBonus * -1f; // 减去当前装备的物品属性
            
        }

        // 5. 如果有想要预览的新装备，则叠加上新装备的属性加成
        if (previewItem != null)
        {
            previewTotal += previewItem.statBonus;
            
            
        }

        // 6. 返回计算完毕后的预览总属性
        return previewTotal;
    }

}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗道具二级面板。
/// 每次打开时读取背包库存，只展示消耗品，不展示装备。
/// 当前版本只展示道具与数量，不执行使用效果或扣除库存。
/// 建议将本控制器挂在始终激活的一级指令面板对象上，panelRoot 指向道具按钮下的 Scroll View 根物体。
/// </summary>
public class BattleItemPanelController : MonoBehaviour
{
    [Header("道具面板")]
    [SerializeField, Tooltip("道具二级面板根物体，建议放在道具按钮下")]
    private GameObject panelRoot;

    [SerializeField, Tooltip("滚动列表的 Content 节点，不是 Scroll View 或 Viewport")]
    private Transform itemListContent;

    [SerializeField, Tooltip("挂载 BattleItemEntryUI 并绑定图标、名称、数量文本的条目预制体")]
    private BattleItemEntryUI itemEntryPrefab;

    [SerializeField, Tooltip("没有道具或缺少数据时显示的提示文本")]
    private TMPro.TMP_Text statusText;

    private readonly List<BattleItemEntryUI> spawnedEntries = new();

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("BattleItemPanelController 未绑定道具面板根物体。", this);
            return;
        }

        bool shouldShow = !panelRoot.activeSelf;
        panelRoot.SetActive(shouldShow);

        if (shouldShow)
            RefreshItems();
    }

    public void ShowPanel()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("BattleItemPanelController 未绑定道具面板根物体。", this);
            return;
        }

        panelRoot.SetActive(true);
        RefreshItems();
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void RefreshItems()
    {
        ClearEntries();

        if (statusText != null)
            statusText.text = string.Empty;

        if (itemListContent == null || itemEntryPrefab == null)
        {
            SetStatus("请绑定道具列表 Content 和道具条目预制体。");
            Debug.LogWarning("道具面板引用不完整：请检查 Item List Content 与 Item Entry Prefab。", this);
            return;
        }

        InventoryMananger inventory = InventoryMananger.Instance;
        if (inventory == null)
        {
            SetStatus("无法读取背包库存。");
            return;
        }

        int consumableCount = 0;
        foreach (InventoryItem item in inventory.CurrentInventory)
        {
            if (item == null || item.ItemDefinition == null || !item.IsConsumable)
                continue;

            BattleItemEntryUI entry = Instantiate(itemEntryPrefab, itemListContent);
            entry.Setup(item);
            spawnedEntries.Add(entry);
            consumableCount++;
        }

        if (consumableCount == 0)
            SetStatus("没有可用道具。");
    }

    private void ClearEntries()
    {
        foreach (BattleItemEntryUI entry in spawnedEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        spawnedEntries.Clear();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}

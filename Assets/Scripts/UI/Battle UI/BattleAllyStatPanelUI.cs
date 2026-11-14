using MFrameork.Event;
using TMPro;
using UnityEngine;

/// <summary>
/// 我方当前行动者的属性面板。
/// 监听 CTBDisplayedActorChangedEvent，和在它左边的战斗立绘完全同步：
/// 我方角色 → 显示属性；敌方行动或没有角色 → 全部留空。
/// </summary>
public class BattleAllyStatPanelUI : MonoBehaviour, IEventReceiver<CTBDisplayedActorChangedEvent>
{
    [Header("攻击与防御")]
    [SerializeField] private TMP_Text physicalAttackText;
    [SerializeField] private TMP_Text physicalDefenseText;
    [SerializeField] private TMP_Text magicalAttackText;
    [SerializeField] private TMP_Text magicalDefenseText;

    [Header("其他")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text evasionText;

    private void OnEnable()
    {
        EventBus.Subscribe<CTBDisplayedActorChangedEvent>(this);
        Clear();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CTBDisplayedActorChangedEvent>(this);
    }

    public void OnEvent(CTBDisplayedActorChangedEvent evt)
    {
        if (evt.Entity == null || !evt.Entity.IsPlayer || evt.Entity.RuntimeData == null)
        {
            Clear();
            return;
        }

        Fill(evt.Entity.RuntimeData);
    }

    private void Fill(CharacterRuntimeData data)
    {
        StatBlock stats = data.GetTotalStats();

        SetValue(physicalAttackText, stats.PAtk);
        SetValue(physicalDefenseText, stats.PDef);
        SetValue(magicalAttackText, stats.MAtk);
        SetValue(magicalDefenseText, stats.MDef);
        SetValue(speedText, stats.Speed);
        SetPercent(critChanceText, data.CritChancePercent);
        SetValue(accuracyText, stats.Accuracy);
        SetValue(evasionText, stats.Evasion);
    }

    private void Clear()
    {
        SetText(physicalAttackText, string.Empty);
        SetText(physicalDefenseText, string.Empty);
        SetText(magicalAttackText, string.Empty);
        SetText(magicalDefenseText, string.Empty);
        SetText(speedText, string.Empty);
        SetText(critChanceText, string.Empty);
        SetText(accuracyText, string.Empty);
        SetText(evasionText, string.Empty);
    }

    private static void SetValue(TMP_Text text, int value)
    {
        SetText(text, value.ToString());
    }

    private static void SetPercent(TMP_Text text, float percent)
    {
        SetText(text, $"{percent:0.#}%");
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}

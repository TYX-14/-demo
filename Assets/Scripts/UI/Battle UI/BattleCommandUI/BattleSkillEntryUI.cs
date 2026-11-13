using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 技能列表中的单个显示条目。
/// 负责显示技能信息、站位图案，并在鼠标悬停时展示技能说明。
/// </summary>
public class BattleSkillEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("可在 Inspector 中分别绑定")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text usablePositionsText;
    [SerializeField] private TMP_Text targetPositionsText;
    [SerializeField] private TMP_Text spCostText;

    [Header("站位图案")]
    [SerializeField] private SkillPositionDiagram positionDiagram;

    [Header("不可用表现")]
    [SerializeField, Range(0f, 1f), Tooltip("站位不合法时的整体透明度，用于提示玩家该技能当前无法使用。")]
    private float unavailableAlpha = 0.45f;

    private GameObject skillTip;
    private TMP_Text skillTipDescription;

    private Button skillButton;
    private SkillDefinitionSO currentSkill;
    private int currentSkillLevel = 1;
    private Action<SkillDefinitionSO> onSkillSelected;
    private Action onUnavailableClicked;
    private CanvasGroup entryCanvasGroup;
    private bool isUsable = true;

    private void Awake()
    {
        skillButton = GetComponent<Button>();
        if (skillButton != null)
            skillButton.onClick.AddListener(HandleSkillClicked);

        entryCanvasGroup = GetComponent<CanvasGroup>();
        if (entryCanvasGroup == null)
            entryCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (positionDiagram == null)
            positionDiagram = GetComponentInChildren<SkillPositionDiagram>(true);

        Transform tipTransform = transform.Find("Skill Tip");
        if (tipTransform == null)
            tipTransform = FindChildByName(transform, "Skill Tip");

        if (tipTransform != null)
        {
            skillTip = tipTransform.gameObject;
            skillTipDescription = skillTip.GetComponentInChildren<TMP_Text>(true);
            skillTip.SetActive(false);

            // Tip 只用于展示，不能挡住按钮的指针事件，否则悬停时可能闪烁。
            foreach (Graphic graphic in skillTip.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

    public void Setup(SkillDefinitionSO skill, Action<SkillDefinitionSO> onSelected = null, bool interactable = true)
    {
        Setup(skill, 1, onSelected, interactable);
    }

    /// <summary>用角色当前拥有的技能等级刷新条目说明。</summary>
    public void Setup(SkillDefinitionSO skill, int skillLevel,
        Action<SkillDefinitionSO> onSelected = null, bool interactable = true,
        Action onUnavailableSelected = null)
    {
        currentSkillLevel = Mathf.Max(1, skillLevel);
        currentSkill = skill;
        onSkillSelected = onSelected;
        onUnavailableClicked = onUnavailableSelected;
        isUsable = interactable;
        if (skill == null)
        {
            Clear();
            return;
        }

        // 站位不合法时仍然保持可点击：玩家点它应当取消上一次技能的目标选择，
        // 只是不会进入这个技能。可点击性与可用性分开，视觉上用透明度提示。
        if (skillButton != null)
            skillButton.interactable = true;

        ApplyAvailabilityVisual();

        if (skillIcon != null)
        {
            skillIcon.sprite = skill.SkillIcon;
            skillIcon.enabled = skill.SkillIcon != null;
        }

        if (skillNameText != null)
            skillNameText.text = skill.SkillName;

        // 站位信息改由八点图案表现，不再把站位编号写成文字。
        if (usablePositionsText != null)
            usablePositionsText.text = string.Empty;

        if (targetPositionsText != null)
            targetPositionsText.text = string.Empty;

        if (spCostText != null)
            spCostText.text = $"{skill.SpCost} SP";

        if (positionDiagram != null)
            positionDiagram.Setup(skill);

        if (skillTipDescription != null)
            skillTipDescription.text = skill.GetDescriptionForLevel(currentSkillLevel) ?? string.Empty;
    }

    private void HandleSkillClicked()
    {
        if (currentSkill == null)
            return;

        if (isUsable)
        {
            onSkillSelected?.Invoke(currentSkill);
            return;
        }

        // 站位不允许使用：不进入该技能，但要让上层取消上一次技能的目标选择。
        onUnavailableClicked?.Invoke();
    }

    /// <summary>按可用性调整条目整体透明度，给玩家一个“现在不能用”的视觉提示。</summary>
    private void ApplyAvailabilityVisual()
    {
        if (entryCanvasGroup != null)
            entryCanvasGroup.alpha = isUsable ? 1f : unavailableAlpha;
    }

    private void OnDestroy()
    {
        if (skillButton != null)
            skillButton.onClick.RemoveListener(HandleSkillClicked);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (positionDiagram != null)
            positionDiagram.SetSelected(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (positionDiagram != null)
            positionDiagram.SetSelected(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (skillTip != null)
            skillTip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (skillTip != null)
            skillTip.SetActive(false);
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void Clear()
    {
        currentSkill = null;
        currentSkillLevel = 1;
        onSkillSelected = null;
        onUnavailableClicked = null;
        isUsable = true;
        ApplyAvailabilityVisual();
        if (skillIcon != null)
        {
            skillIcon.sprite = null;
            skillIcon.enabled = false;
        }

        if (skillNameText != null)
            skillNameText.text = string.Empty;

        if (usablePositionsText != null)
            usablePositionsText.text = string.Empty;

        if (targetPositionsText != null)
            targetPositionsText.text = string.Empty;

        if (spCostText != null)
            spCostText.text = string.Empty;

        if (positionDiagram != null)
            positionDiagram.Clear();

        if (skillTipDescription != null)
            skillTipDescription.text = string.Empty;

        if (skillTip != null)
            skillTip.SetActive(false);
    }
}

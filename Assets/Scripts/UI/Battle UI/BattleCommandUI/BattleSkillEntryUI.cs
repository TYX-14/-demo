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

    private GameObject skillTip;
    private TMP_Text skillTipDescription;

    private Button skillButton;
    private SkillDefinitionSO currentSkill;
    private Action<SkillDefinitionSO> onSkillSelected;

    private void Awake()
    {
        skillButton = GetComponent<Button>();
        if (skillButton != null)
            skillButton.onClick.AddListener(HandleSkillClicked);

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
        currentSkill = skill;
        onSkillSelected = onSelected;
        if (skill == null)
        {
            Clear();
            return;
        }

        // 站位不允许使用时禁用按钮，避免玩家点击后才收到拒绝反馈。
        if (skillButton != null)
            skillButton.interactable = interactable;

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
            skillTipDescription.text = skill.Description ?? string.Empty;
    }

    private void HandleSkillClicked()
    {
        if (currentSkill != null && skillButton != null && skillButton.interactable)
            onSkillSelected?.Invoke(currentSkill);
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
        onSkillSelected = null;
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

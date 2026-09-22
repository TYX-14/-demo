
using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentCharacterTabButton : MonoBehaviour , ISelectHandler
{
    [SerializeField] private Image portraitImage;

    [SerializeField] private CanvasGroup canvasGroup;

    private Button _button;
    private int _index;

    private Action<int> _onSelect;
    private Action<int> _onCliked;

    public Button Button => _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClick);
        
        
    }
    /// <summary>
    /// 设置Tab，拿到回调函数
    /// </summary>
    /// <param name="meber"></param>
    /// <param name="index"></param>
    /// <param name="onSelect"></param>
    /// <param name="onCliked"></param>
    public void SetUp(CharacterRuntimeData meber, int index,Action<int> onSelect, Action<int> onCliked)
    {
        _index = index;
        _onSelect = onSelect;
        _onCliked = onCliked;

        if (portraitImage != null)
        {
            portraitImage.sprite = meber.Definition.LeftIdle;
        }

    }



    /// <summary>
    /// 设置被tab选中透明度
    /// </summary>
    /// <param name="selectedd"></param>
    public void SetSelecteVisual(bool selectedd)
    {
        canvasGroup.alpha = selectedd ? 1 : 0.5f;
    }
    /// <summary>
    /// 设置tab可交互性
    /// </summary>
    /// <param name="interactable"></param>
    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
    }


    /// <summary>
    /// 绑定点击
    /// </summary>
    private void HandleClick()
    {
        
        _onCliked?.Invoke(_index);
    }

    public void OnSelect(BaseEventData eventData)
    {
        
        _onSelect?.Invoke(_index);
    }


  
}

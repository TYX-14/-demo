using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
public class ActionMeunButton : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _buttontext;
    [SerializeField] private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    public void SetButton(ActionCommandInfo commandInfo, UnityAction onClick)
    {
        _icon.sprite = commandInfo.Icon;
        _buttontext.text = commandInfo.displayName;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(onClick);
        }
        
    }
}

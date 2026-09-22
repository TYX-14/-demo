
using TMPro;

public class EquipmentStatCompareRow : MonoBehaviour
{
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text modifyValueText;

    [Header("颜色")]
    [SerializeField] private Color increaseColor;
    [SerializeField] private Color decreaseColor;

    /// <summary>
    /// 设置比对预览
    /// </summary>
    /// <param name="currentValue"></param>
    /// <param name="previewValue"></param>
    /// <param name="isPreviewMode"></param>
    public void SetRaw(int currentValue, int previewValue, bool isPreviewMode = true)
    {
        currentValueText.text = currentValue.ToString();
        
        if (!isPreviewMode || previewValue == currentValue)
        {
            modifyValueText.text = "";
           
            return;
        }
       

        
        //颜色
        if (previewValue > currentValue)
        {
            modifyValueText.color = increaseColor;
            modifyValueText.text = "↑" + (previewValue-currentValue).ToString();
        }
        else
        {
            modifyValueText.color = decreaseColor;
            modifyValueText.text = "↓" + (previewValue - currentValue).ToString();
        }
    }

       
    
}

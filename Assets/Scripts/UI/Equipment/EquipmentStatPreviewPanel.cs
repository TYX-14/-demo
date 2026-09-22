

using System;

public class EquipmentStatPreviewPanel : MonoBehaviour
{
    [Serializable]
    public struct StatRowBinding
    {
        public StatType statType;
        public EquipmentStatCompareRow row;
    }

    [SerializeField] private StatRowBinding[] statRows;

    /// <summary>
    /// 刷新显示
    /// </summary>
    /// <param name="currentStat"></param>
    /// <param name="previewStats"></param>
    /// <param name="isInPreviewMode"></param>
    public void Refresh(StatBlock currentStat, StatBlock previewStats,bool isInPreviewMode = true)
    {
        
        for (int i = 0; i < statRows.Length; i++)
        {
            var binding = statRows[i];

            int current = ReadStat(currentStat, binding.statType);
            int preview = ReadStat(previewStats, binding.statType);
            //设置row
            
            binding.row.SetRaw(current, preview);
        }
    }


    /// <summary>
    /// 读取属性
    /// </summary>
    /// <param name="statBlock"></param>
    /// <param name="statType"></param>
    /// <returns></returns>
    private int ReadStat(StatBlock statBlock,StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHP: return statBlock.MaxHP;
            case StatType.MaxSP: return statBlock.MaxSP;
            case StatType.PActk: return statBlock.PAtk;
            case StatType.PDef: return statBlock.PDef;
            case StatType.MAtk: return statBlock.MAtk;
            case StatType.MDef: return statBlock.MDef;
            case StatType.Speed: return statBlock.Speed;
            case StatType.Accuracy: return statBlock.Accuracy;
            case StatType.Evasion: return statBlock.Evasion;
           default : return 0;

        }
    }
}

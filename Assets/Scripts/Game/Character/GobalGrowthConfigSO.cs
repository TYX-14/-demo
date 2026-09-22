[CreateAssetMenu(menuName = "Game/GlobalGrowthConfig")]
public class GobalGrowthConfigSO : ScriptableObject
{
    [Header("成长曲线（X：等级1-99，Y：经验值）")]
    public AnimationCurve RankS = AnimationCurve.Linear(1, 1, 99, 4f);
    public AnimationCurve RankA = AnimationCurve.Linear(1, 1, 99, 3f);
    public AnimationCurve RankB = AnimationCurve.Linear(1, 1, 99, 2.5f);
    public AnimationCurve RankC = AnimationCurve.Linear(1, 1, 99, 2f);
    public AnimationCurve RankD = AnimationCurve.Linear(1, 1, 99, 1.5f);

    /// <summary>
    /// 参数成长曲线的枚举，返回对应的成长曲线
    /// </summary>
    /// <param name="rank"></param>
    /// <returns></returns>
    public AnimationCurve GetCurveByRank(GrowthRank rank)
    {
        return rank switch
        {
            GrowthRank.S => RankS,
            GrowthRank.A => RankA,
            GrowthRank.B => RankB,
            GrowthRank.C => RankC,
            GrowthRank.D => RankD,
            _=>RankB
        };
    }
}

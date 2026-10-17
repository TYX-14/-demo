/// <summary>
/// 用于创建跟随者到场景中
/// </summary>
public class PartyFieldController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followersParent;
    [SerializeField] private GameObject fieldFollowerPrefab;
    [SerializeField] private Transform playerTrans;

    [Header("Setting")]
    [SerializeField] private float followDistance=1.2f;//相邻的两人的距离
    [SerializeField] private float followSpeed = 5f;

    [SerializeField] private float z0ffset = 0.01f;
    [SerializeField] private float sampleMinDistance = 0.1f;//最小采样值

    private List<Vector3> trail = new List<Vector3>();//记录移动轨迹
    private List<FieldFollower> fieldFollowers = new();//跟随者


    private void LateUpdate()
    {
        UpdateLeaderTrail();

        for (int i = 0; i < fieldFollowers.Count; i++)
        {
            var follower = fieldFollowers[i];
            float targetDistance = followDistance * (i + 1);
            Vector3 tatgetPos = GetPointAtDistance(targetDistance);
            follower.MoveTo(tatgetPos, followSpeed);
        }


    }
    /// <summary>
    /// 根据参数（和领头人的距离）得到在轨迹上的点
    /// </summary>
    /// <param name="distanceFromLeder"></param>
    /// <returns></returns>
    private Vector3 GetPointAtDistance(float distanceFromLeder)
    {
        if (trail.Count == 0)
        {
            return playerTrans.position;
        }

        float accumulated = 0f;
        for (int i = 0; i < trail.Count-1; i++)
        {
            Vector3 a = trail[i];//当前点
            Vector3 b = trail[i + 1];//下一个点
            float dist = Vector3.Distance(a, b);//计算两点距离


            //如果“已走的路程 + 当前线段的长度”大于或等于“目标距离”，说明我们要找的那个点就在这条线段（a 到 b）之间。
            if (dist + accumulated >= distanceFromLeder)
            {
                
                float t = (distanceFromLeder - accumulated) / dist;
                return Vector3.Lerp(a, b, t);
            }
            accumulated += dist;
        }
        //如果所有点的距离总和任然小于目标距离，那么返回最后一个点
        return trail[trail.Count - 1];
    }

    /// <summary>
    /// 把目标跟随者加入创建列表
    /// </summary>
    /// <param name="partyMembers"></param>
    public void UpdateFollowers(List<CharacterDefinitionSO> partyMembers, bool resetPositions = true)
    {
        int followerCount = partyMembers.Count - 1;//人数减去主角
        while (fieldFollowers.Count < followerCount)
        {
            int index = fieldFollowers.Count;
            var pos = ApplyFollowerOffset(playerTrans.position, index);
            GameObject followerObj = Instantiate(fieldFollowerPrefab, pos, Quaternion.identity, followersParent);

            fieldFollowers.Add(followerObj.GetComponent<FieldFollower>());
        }

        for (int i = 0; i < followerCount; i++)
        {
            fieldFollowers[i].SetupFollower(partyMembers[i + 1]);
        }

        if (resetPositions)
        {
            RebuildTrailAndSnapFollowers();
        }
    }
    /// <summary>
    /// 创建跟随者实例到指定坐标
    /// </summary>
    /// <param name="position"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    private Vector3 ApplyFollowerOffset(Vector3 position,int index)
    {
        position.z += z0ffset * (index + 1);
        return position;
    }
    /// <summary>
    /// 用于记录玩家（领头人）轨迹
    /// </summary>
    private void UpdateLeaderTrail()
    {
        Vector3 leaderPos = playerTrans.position;
        if (trail.Count == 0)
        {
            trail.Add(leaderPos);
            return;
        }
        //计算玩家当前位置与轨迹列表中第一个点（也就是上一次记录的位置 trail[0]）之间的直线距离
        float dist = Vector3.Distance(leaderPos, trail[0]);

        if (dist > sampleMinDistance)
        {
            //这行代码的意思是：把新获取的玩家位置，强行插到列表的第 1 位（即索引 0 的位置）
            trail.Insert(0, leaderPos);
            if (trail.Count > 100)
            {
                trail.RemoveAt(trail.Count - 1);
            }
        }
    }
    /// <summary>
    /// 用于在刷新，进入新场景时，重置跟随者（让他们紧贴在一起）
    /// </summary>
    private void RebuildTrailAndSnapFollowers()
    {
        trail.Clear();

        for (int i = 0; i < fieldFollowers.Count; i++)
        {
            fieldFollowers[i].SnapTo(ApplyFollowerOffset(playerTrans.position,i));
        }
        UpdateLeaderTrail();
    }

    /// <summary>
    /// 设置玩家物体的激活（显隐）状态。
    /// 通常用于战斗模式与探索模式切换时的角色隐藏与恢复。
    /// </summary>
    /// <param name="active">True 表示激活显示，False 表示隐藏禁用。</param>
    public void SetPlayerActive(bool active)
    {
        playerTrans.gameObject.SetActive(active);
    }

    /// <summary>
    /// 清除场上所有的跟随者单位，并清空对应的轨迹缓存。
    /// </summary>
    public void ClearFollower()
    {
        foreach (var follower in fieldFollowers)
        {
            if (follower != null)
                Destroy(follower.gameObject);
        }
        fieldFollowers.Clear();
        trail.Clear();
    }
}

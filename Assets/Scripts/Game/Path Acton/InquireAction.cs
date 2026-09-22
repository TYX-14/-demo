
public class InquireAction : ActionBase
{
    [Header("随机消息设置")]
    [SerializeField] private List<InquireActionData> inquireActionDatas = new List<InquireActionData>();

    public int PickRandomMessageIndex() => UnityEngine.Random.Range(0, inquireActionDatas.Count);//随机选择一条信息

    public void GetInquireActionData(int index,out InquireActionData inquireActionData) => inquireActionData = inquireActionDatas[index];

    public override void TriggerAction(AllyDefinitionSO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }
}

[System.Serializable]
public class InquireActionData
{
    [Header("消息信息")]
    public string title;
    public string personName;
    [TextArea(2,6)]
    public string message;
    public Sprite SpriteOverrdie;
}

public class RecruitiActon : ActionBase
{
    public CharacterDefinitionSO CurrentCharacter{  get; private set; }

    private void Awake()
    {
        CurrentCharacter = GetComponent<CharacterIdentity>().CharacterDefinition;

    }

    public override void TriggerAction(AllyDefinitionSO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }

    public override void Execute(object contextData = null)
    {
        
        PartyManager.Instance.RecuitMember(CurrentCharacter);
        HideSceneNPC();
       
    }

    private void HideSceneNPC()
    {
        this.gameObject.SetActive(false);
    }
}

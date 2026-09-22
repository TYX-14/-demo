
public class Playerinteractor : MonoBehaviour
{
    private CharacterIdentity _characterIdenitity;//角色基本信息

    private InteractionBase _target;//缓存发生交互的npc

    private void Awake()
    {
        _characterIdenitity = GetComponent<CharacterIdentity>();
    }

    private void Update()
    {
        var input = InputSystemController.Instance;

        if (input == null) { return; }

        if (input.GetPlayerConfirmPressed()&&_target!=null)
        {
            _target.Interact(_characterIdenitity.CharacterDefinition as AllyDefinitionSO);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out InteractionBase interactionBase))
        {
            _target = interactionBase;
            interactionBase.OnFocus(_characterIdenitity.CharacterDefinition as AllyDefinitionSO);
            

        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out InteractionBase interactionBase))
        {
            
            interactionBase.OnLoseFocus();
            _target = null;
        }
    }
}

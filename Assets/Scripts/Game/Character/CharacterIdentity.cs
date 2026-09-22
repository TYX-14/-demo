
public class CharacterIdentity : MonoBehaviour
{
    [SerializeField] private CharacterDefinitionSO _characteDefinition;

    public CharacterDefinitionSO CharacterDefinition => _characteDefinition;

    public void SetDefinition(CharacterDefinitionSO characteDefinition)
    {
        _characteDefinition = characteDefinition;
    }
}

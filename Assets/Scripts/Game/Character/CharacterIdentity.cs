
/// <summary>
/// 角色身份组件。
/// </summary>
public class CharacterIdentity : MonoBehaviour
{
    [SerializeField] private CharacterDefinitionSO _characteDefinition;

    public CharacterDefinitionSO CharacterDefinition => _characteDefinition;
/// <summary>
/// 设置角色定义
/// </summary>
/// <param name="characteDefinition"></param>
    public void SetDefinition(CharacterDefinitionSO characteDefinition)
    {
        _characteDefinition = characteDefinition;
    }
}

using UnityEngine.InputSystem;
using MFrameork.Event;

public class InputSystemController : singleton<InputSystemController>,IEventReceiver<GameModeChangedEvent>
{
    private CharacterInputAction _inputActions;
    private CharacterInputAction InputActions => _inputActions;

    private ActiveMap _currenMap = ActiveMap.None;

    private bool _isInitalized = false;


    protected override void Awake()
    {
        base.Awake();
        if (!_isInitalized)
        {
            _inputActions = new CharacterInputAction();
            _isInitalized = true;
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
        _inputActions.Player.Enable();
    }
    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        _inputActions.Player.Disable();
    }
    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    public Vector2 GetMovementInput()
    {
        if (!_isInitalized || _currenMap != ActiveMap.Player)
        {
            return Vector2.zero;
        }
        return _inputActions.Player.Move.ReadValue<Vector2>();
    }

    public bool GetPlayerConfirmPressed()
    {
        if (!_isInitalized || _currenMap != ActiveMap.Player)
        {
            return false;
        }
        return _inputActions.Player.Confirm.WasPressedThisFrame();
    }

    public bool GetUIancelPressed()
    {
        if (!_isInitalized || _currenMap != ActiveMap.UI)
        {
            return false ;
        }
        return _inputActions.UI.Cancel.WasPressedThisFrame();

    }

    public bool GetMenuPressed()
    {
        if (!_isInitalized)
        {
            return false;;
        }
        return _inputActions.UI.Menu.WasPressedThisFrame()||_inputActions.Player.Menu.WasPressedThisFrame();
    }


    #region 事件实现
    public void OnEvent(GameModeChangedEvent evt)
    {
        Debug.Log(evt.newMode.ToString());
        _currenMap = GetMapFromMode(evt.newMode);

        switch (_currenMap)
        {
            case ActiveMap.Player:
                _inputActions.Player.Enable();
                _inputActions.UI.Disable();
                break;
            case ActiveMap.UI:
                _inputActions.UI.Enable();
                _inputActions.Player.Disable();
                break;
            case ActiveMap.None:
            default:
                break;

        }
    }
    #endregion

    private ActiveMap GetMapFromMode(GameMode gameMode)
    {
        switch (gameMode)
        {
            case GameMode.Battle:
            case GameMode.InteractionMenu:
            case GameMode.Pause:
                return ActiveMap.UI;
            case GameMode.Explore:
            default:
                return ActiveMap.Player;
        }
    }
}

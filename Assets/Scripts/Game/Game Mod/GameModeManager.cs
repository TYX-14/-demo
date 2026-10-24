
public class GameModeManager : singleton<GameModeManager>
{
    public GameMode CurrentGameMode;

   [SerializeField] private GameMode defaultMode = GameMode.Explore;

  
    protected override void Awake()
    {
        base.Awake();
        CurrentGameMode = defaultMode;
    }
    private void Start()
    {
        //šă˛ĽľąÇ°ÓÎĎˇÄŁĘ˝
        AppleMode(CurrentGameMode);
    }

    public void RequestChangeMode(GameMode newMode)
    {
        
        if (Instance == null)
        {
            return;
        }
        if (CanSwitchMode(newMode))
        {
            AppleMode(newMode);
           
            
        }
    }

    public bool CanSwitchMode(GameMode newMode)
    {
        if (CurrentGameMode == GameMode.Battle)
        {
            return false;
        }
        return true;
    }

    private void AppleMode(GameMode newMode)
    {
        CurrentGameMode = newMode;
        EventBus.Publish(new GameModeChangedEvent(CurrentGameMode));
    }
}

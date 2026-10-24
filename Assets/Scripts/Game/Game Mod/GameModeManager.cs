
public class GameModeManager : singleton<GameModeManager>
{
    public GameMode CurrentGameMode;

    /// <summary>
    /// 暂停前的模式，用于恢复探索或战斗。
    /// </summary>
    public GameMode ModeBeforePause { get; private set; } = GameMode.Explore;

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
        if (Instance == null || newMode == CurrentGameMode)
        {
            return;
        }

        if (!CanSwitchMode(newMode))
        {
            return;
        }

        if (newMode == GameMode.Pause)
        {
            ModeBeforePause = CurrentGameMode;
        }

        AppleMode(newMode);
    }

    public bool CanSwitchMode(GameMode newMode)
    {
        // 战斗中只能进入暂停，不能直接跳回探索。
        if (CurrentGameMode == GameMode.Battle)
        {
            return newMode == GameMode.Pause;
        }

        // 暂停期间只允许恢复到进入暂停前的模式。
        if (CurrentGameMode == GameMode.Pause)
        {
            return newMode == ModeBeforePause;
        }

        return true;
    }

    private void AppleMode(GameMode newMode)
    {
        CurrentGameMode = newMode;
        EventBus.Publish(new GameModeChangedEvent(CurrentGameMode));
    }
}

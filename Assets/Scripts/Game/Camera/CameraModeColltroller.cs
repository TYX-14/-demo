using MFrameork.Event;
public class CameraModeColltroller : MonoBehaviour, IEventReceiver<GameModeChangedEvent>
{
    [Header("Па»ъ")]
    [SerializeField] private GameObject followCamera;
    [SerializeField] private GameObject battleCamera;



    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }
    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        switch (evt.newMode)
        {
            case GameMode.Explore:
                SetCamerView(CameraView.Explore);
                break;
            case GameMode.Battle:
                SetCamerView(CameraView.Battle);
                break;
        }
    }



    private void SetCamerView(CameraView view)
    {
        bool followActive = false;
        bool battleActive = false;
        switch (view)
        {
            case CameraView.Explore:
                followActive = true;
                break;
            case CameraView.Battle:
                battleActive = true;
                break;
        }
        followCamera.SetActive(followActive);
        battleCamera.SetActive(battleActive);
    }
}

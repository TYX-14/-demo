using System.Collections;
using MFrameork.Event;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 探索 / 战斗相机切换。
/// 切相机本身仍交给 CinemachineBrain，但过渡的“起点”由这里显式钉住：
/// 先用一台只在运行时存在的快照相机接管当前画面，再从快照过渡到目标相机。
/// 这样即使退出相机被停用、跟随目标被隐藏，大脑也不会取到退化的起始位姿
/// （表现为镜头从地下或远处升起）。
/// </summary>
public class CameraModeColltroller : MonoBehaviour, IEventReceiver<GameModeChangedEvent>
{
    /// <summary>快照相机的优先级，必须高于场景里两台虚拟相机（它们默认都是 0）。</summary>
    private const int SnapshotPriority = 1000;

    [Header("相机")]
    [SerializeField] private GameObject followCamera;
    [SerializeField] private GameObject battleCamera;

    [Header("过渡快照")]
    [SerializeField, Tooltip("可选的快照相机。留空时会在首次切换时自动创建一个仅存在于运行时的相机。")]
    private CinemachineCamera transitionSnapshotCamera;

    [SerializeField, Tooltip("排查用：每次切换在 Console 打印被钉住的起点位姿。确认稳定后可关闭。")]
    private bool logTransition = true;

    private CinemachineCamera _runtimeSnapshot;
    private bool _switching;

    /// <summary>切换淡出开始时记下的视角。过渡期间相机会乱跑，只能用这个干净时刻的位姿。</summary>
    private bool _anchorValid;
    private Vector3 _anchorPosition;
    private Quaternion _anchorRotation;
    private LensSettings _anchorLens;
    private float _anchorTime;

    /// <summary>锚点有效期。超过这个时间说明不是同一次场景过渡，退回实时采样。</summary>
    private const float AnchorLifetime = 20f;

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);

        // 切换途中被禁用时不要把快照留在激活状态，否则画面会停在快照上。
        if (_runtimeSnapshot != null && _runtimeSnapshot.gameObject.activeSelf)
            _runtimeSnapshot.gameObject.SetActive(false);

        _switching = false;
    }

    private void OnDestroy()
    {
        if (_runtimeSnapshot != null)
            Destroy(_runtimeSnapshot.gameObject);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        switch (evt.newMode)
        {
            // 场景过渡的第一步就是切到 Pause，此刻画面还是干净的探索/战斗视角。
            // 之后会淡出、卸载旧场景，相机可能在黑屏期间跟着掉下去的玩家一起沉到地下，
            // 所以起点必须在这里就固定下来。
            case GameMode.Pause:
                CaptureTransitionAnchor();
                break;
            case GameMode.Explore:
                SetCamerView(CameraView.Explore);
                break;
            case GameMode.Battle:
                SetCamerView(CameraView.Battle);
                break;
        }
    }

    /// <summary>记录淡出瞬间真正渲染的视角，作为下一次切换的过渡起点。</summary>
    private void CaptureTransitionAnchor()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            _anchorValid = false;
            return;
        }

        _anchorPosition = mainCamera.transform.position;
        _anchorRotation = mainCamera.transform.rotation;

        CinemachineCamera runningCamera = FindRunningCamera();
        _anchorLens = runningCamera != null ? runningCamera.Lens : LensSettings.Default;

        _anchorTime = Time.unscaledTime;
        _anchorValid = true;

        if (logTransition)
            Debug.Log($"[相机过渡] 已在淡出时刻记录起点 {_anchorPosition}", this);
    }

    private void SetCamerView(CameraView view)
    {
        GameObject incoming = view == CameraView.Battle ? battleCamera : followCamera;
        GameObject outgoing = view == CameraView.Battle ? followCamera : battleCamera;

        if (incoming == null || outgoing == null)
        {
            Debug.LogWarning("CameraModeColltroller：跟随相机或战斗相机未在 Inspector 中配置。", this);
            return;
        }

        // 已经在目标视角上，不必重复切换（避免重播一次 blend）。
        if (incoming.activeSelf && !outgoing.activeSelf)
            return;

        if (_switching)
            return;

        StartCoroutine(SwitchRoutine(outgoing, incoming));
    }

    /// <summary>
    /// 两步切换：
    /// 1. 把当前画面钉进快照相机、停用退出相机（这一帧画面完全不变）；
    /// 2. 下一帧启用目标相机、停用快照，大脑就从快照位姿平滑过渡到目标位姿。
    /// 终点位置、缓动曲线与时长都没有改动，仍由 CinemachineBrain 决定。
    /// </summary>
    private IEnumerator SwitchRoutine(GameObject outgoing, GameObject incoming)
    {
        _switching = true;

        if (!TryCaptureCurrentView(out CinemachineCamera snapshot, out Vector3 capturedPosition,
                out float capturedFov, out bool fromAnchor))
        {
            // 兜底：拿不到快照就退回原来的直接切换，至少不会卡住流程。
            outgoing.SetActive(false);
            incoming.SetActive(true);
            _switching = false;
            yield break;
        }

        if (logTransition)
        {
            Debug.Log($"[相机切换] {outgoing.name} → {incoming.name}；" +
                      $"起点 {capturedPosition}（{(fromAnchor ? "淡出锚点" : "实时采样")}），" +
                      $"FOV {capturedFov:F1}", this);
        }

        outgoing.SetActive(false);

        // 等一帧，让大脑把快照认作当前相机。这一帧画面与切换前一致。
        yield return null;

        incoming.SetActive(true);

        if (snapshot != null)
            snapshot.gameObject.SetActive(false);

        _switching = false;
    }

    /// <summary>
    /// 把过渡起点写进快照相机。
    /// 优先使用淡出时刻记录的锚点；没有锚点（例如开局第一次切相机）才实时采样。
    /// </summary>
    private bool TryCaptureCurrentView(
        out CinemachineCamera snapshot,
        out Vector3 position,
        out float fieldOfView,
        out bool fromAnchor)
    {
        snapshot = null;
        position = Vector3.zero;
        fieldOfView = 0f;
        fromAnchor = false;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        snapshot = EnsureSnapshotCamera();
        if (snapshot == null)
            return false;

        fromAnchor = _anchorValid && Time.unscaledTime - _anchorTime <= AnchorLifetime;

        if (fromAnchor)
        {
            position = _anchorPosition;
            snapshot.transform.SetPositionAndRotation(_anchorPosition, _anchorRotation);
            // 镜头参数同样取淡出时刻的，避免黑屏期间虚拟相机被改动导致视野跳变。
            snapshot.Lens = _anchorLens;
        }
        else
        {
            position = mainCamera.transform.position;
            snapshot.transform.SetPositionAndRotation(position, mainCamera.transform.rotation);

            CinemachineCamera runningCamera = FindRunningCamera();
            if (runningCamera != null)
                snapshot.Lens = runningCamera.Lens;
        }

        fieldOfView = snapshot.Lens.FieldOfView;

        snapshot.Priority = SnapshotPriority;
        snapshot.PreviousStateIsValid = false;
        snapshot.gameObject.SetActive(true);

        // 立刻把位姿写进状态：即使大脑本帧晚一步采样，快照也已经是一个有效位姿，
        // 不会退化成默认的 (0,0,0)。
        snapshot.InternalUpdateCameraState(Vector3.up, -1f);

        return true;
    }

    private CinemachineCamera EnsureSnapshotCamera()
    {
        if (transitionSnapshotCamera != null)
            return transitionSnapshotCamera;

        if (_runtimeSnapshot != null)
            return _runtimeSnapshot;

        GameObject snapshotObject = new GameObject("Camera Transition Snapshot");
        _runtimeSnapshot = snapshotObject.AddComponent<CinemachineCamera>();
        _runtimeSnapshot.Priority = SnapshotPriority;
        snapshotObject.SetActive(false);

        return _runtimeSnapshot;
    }

    /// <summary>取当前正在驱动画面、且已激活的那台虚拟相机，用来复制镜头参数。</summary>
    private CinemachineCamera FindRunningCamera()
    {
        if (followCamera != null && followCamera.activeSelf)
        {
            CinemachineCamera follow = followCamera.GetComponent<CinemachineCamera>();
            if (follow != null)
                return follow;
        }

        if (battleCamera != null && battleCamera.activeSelf)
        {
            CinemachineCamera battle = battleCamera.GetComponent<CinemachineCamera>();
            if (battle != null)
                return battle;
        }

        return null;
    }
}

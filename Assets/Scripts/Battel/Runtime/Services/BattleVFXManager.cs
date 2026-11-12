using System;
using System.Collections.Generic;
using MFrameork.Event;
using UnityEngine;

/// <summary>
/// 统一管理战斗中的地面光圈（当前行动者标记）。
/// 本组件应放在 Persistent 场景中的常驻对象上；位置只读取 BattleFieldLayout 的槽位数据，
/// 不依赖 BattleUnit 的 Transform。
/// 显示时机：
/// 1. 敌方回合开始时立即在敌方行动者脚下显示换色光圈；
/// 2. 玩家回合不显示，等玩家真正选出技能、进入目标选择时才显示蓝色光圈，
///    取消技能选择则立即收起。
/// 每个光圈自带顶光与地光两盏点光源：顶光按角色精灵渲染边界定位到头顶，
/// 并沿世界 Z 轴朝镜头偏移，避免光源正对像素图打出高光。
/// </summary>
public sealed class BattleVFXManager : MonoBehaviour, IEventReceiver<CTBDisplayedActorChangedEvent>, IEventReceiver<BattleEntityPositionChangedEvent>
{
    public static BattleVFXManager Instance { get; private set; }

    [Header("光圈预制体")]
    [SerializeField, Tooltip("玩家当前行动者使用的蓝色光圈。")]
    private ParticleSystem blueCurrentActorPrefab;
    [SerializeField, Tooltip("敌方当前行动者使用的光圈，建议直接拖入蓝圈的换色版本。")]
    private ParticleSystem enemyCurrentActorPrefab;

    [Header("缩放与位置")]
    [SerializeField, Min(0.01f), Tooltip("所有光圈的基础缩放倍率，可在 Inspector 中调整测试。")]
    private float baseScale = 0.5f;
    [SerializeField, Tooltip("可微调光圈相对槽位中心的世界坐标偏移。默认保持为零。")]
    private Vector3 worldPositionOffset = Vector3.zero;

    [Header("光圈顶光")]
    [SerializeField, Tooltip("关闭后光圈不再生成顶光。")]
    private bool enableActionLight = true;
    [SerializeField, Tooltip("预制体自带的 Point Light 是否也用下面的参数覆盖。\n" +
                             "关闭时保留预制体里调好的颜色与强度，只接管位置。")]
    private bool overridePrefabLightSettings = false;
    [SerializeField, Tooltip("管理器自己创建顶光时使用的颜色。")]
    private Color actionLightColor = new Color(1f, 0.95f, 0.85f, 1f);
    [SerializeField, Min(0f), Tooltip("管理器自己创建顶光时使用的强度。")]
    private float actionLightIntensity = 1.5f;
    [SerializeField, Min(0.01f), Tooltip("管理器自己创建顶光时使用的半径（世界单位）。")]
    private float actionLightRange = 2.5f;
    [SerializeField, Min(0f), Tooltip("顶光在精灵图顶部之上再抬高多少，避免贴着头顶。")]
    private float actionLightTopGap = 0.45f;
    [SerializeField, Min(0f), Tooltip("顶光沿世界 Z 轴朝镜头方向偏移的距离。设为 0 则光源与角色同一平面。")]
    private float actionLightCameraZOffset = 0.3f;
    [SerializeField, Tooltip("管理器自建顶光是否投射阴影。像素风场景通常关闭以节省性能。")]
    private bool actionLightCastShadows = false;
    [SerializeField, Min(0.1f), Tooltip("取不到角色精灵边界时的兜底顶光高度。")]
    private float actionLightFallbackHeight = 1.6f;

    [Header("光圈地光")]
    [SerializeField, Tooltip("关闭后不再生成光圈下方的补光。")]
    private bool enableGroundLight = true;
    [SerializeField, Tooltip("地光颜色。建议比顶光暗一点，避免两盏光把地面照白。")]
    private Color groundLightColor = new Color(1f, 0.92f, 0.8f, 1f);
    [SerializeField, Min(0f), Tooltip("地光强度。")]
    private float groundLightIntensity = 1f;
    [SerializeField, Min(0.01f), Tooltip("地光半径（世界单位）。")]
    private float groundLightRange = 2f;
    [SerializeField, Tooltip("地光相对光圈落点的垂直偏移。负值向下（落在特效底下），正值向上。")]
    private float groundLightOffsetY = -0.25f;
    [SerializeField, Tooltip("地光是否投射阴影。")]
    private bool groundLightCastShadows = false;

    private readonly List<ParticleSystem> _playerRingPool = new();
    private readonly List<ParticleSystem> _enemyRingPool = new();

    private ParticleSystem _activeRing;
    private BattleEntity _activeRingEntity;
    private BattleEntity _displayedActor;
    private BattleFieldLayout _layout;

    // 当前光圈实例上的两盏点光源。跟随实例复用，避免每帧查找组件。
    private Light _topLight;
    private Light _groundLight;
    private bool _topLightCreatedByManager;

    /// <summary>交互标记专用图层，与 BattleUnit 上的 markerLayerName 一致。</summary>
    private const string MarkerLayerName = "BattleMarker";

    private int _markerLayer = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("场景中存在重复的 BattleVFXManager，重复对象将被销毁。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Subscribe<BattleEntityPositionChangedEvent>(this);

        // 标记图层由 BattleMarkerRenderFeature 在后处理之后重绘，
        // 因此这里让所有游戏相机跳过该图层，避免标记被景深糊掉一次。
        _markerLayer = LayerMask.NameToLayer(MarkerLayerName);
        if (_markerLayer >= 0)
            Camera.onPreCull += ExcludeMarkerLayerFromCamera;
        else
            Debug.LogWarning($"BattleVFXManager：未找到图层「{MarkerLayerName}」，战斗标记将仍受景深影响。", this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CTBDisplayedActorChangedEvent>(this);
        EventBus.Unsubscribe<BattleEntityPositionChangedEvent>(this);

        if (_markerLayer >= 0)
            Camera.onPreCull -= ExcludeMarkerLayerFromCamera;

        _displayedActor = null;
        HideCurrentActorRing();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 每帧剔除前把标记图层从游戏相机的剔除掩码里摘掉。
    /// 相机不渲染它，标记只会由 BattleMarkerRenderFeature 在景深之后绘制一次。
    /// </summary>
    private void ExcludeMarkerLayerFromCamera(Camera camera)
    {
        if (camera == null || camera.cameraType != CameraType.Game)
            return;

        // Feature 未启用时不要剔除，否则标记会彻底看不见。
        if (!BattleMarkerRenderFeature.IsActive)
            return;

        camera.cullingMask &= ~(1 << _markerLayer);
    }

    /// <summary>
    /// 当前行动者切换：
    /// 敌方立即显示换色光圈；玩家先不显示，等玩家选出技能后由 TargetSelectionState 调用显示。
    /// </summary>
    public void OnEvent(CTBDisplayedActorChangedEvent evt)
    {
        _displayedActor = evt.Entity;
        HideCurrentActorRing();

        if (evt.Entity == null)
            return;

        if (!evt.Entity.IsPlayer)
            ShowCurrentActorRing(evt.Entity);
    }

    /// <summary>当前行动者位移后同步光圈位置与光源，不重播粒子。</summary>
    public void OnEvent(BattleEntityPositionChangedEvent evt)
    {
        if (evt.Entity == null || evt.Entity != _displayedActor || _activeRing == null)
            return;

        if (TryGetWorldPosition(evt.Entity, out Vector3 position, out _))
        {
            _activeRing.transform.position = position;
            _activeRingEntity = evt.Entity;
            PlaceActionLights(evt.Entity, position);
        }
    }

    /// <summary>
    /// 精灵动画换帧会改变精灵边界，角色也可能被位移，因此每帧重新对齐两盏光源。
    /// 光圈本身仍由事件驱动，这里只更新光源位置。
    /// </summary>
    private void LateUpdate()
    {
        if (_activeRing == null || !_activeRing.gameObject.activeInHierarchy)
            return;

        PlaceActionLights(_activeRingEntity, _activeRing.transform.position);
    }

    /// <summary>
    /// 显示当前行动者的光圈。玩家单位用蓝色，敌方单位用 Inspector 中配置的换色版本。
    /// </summary>
    public void ShowCurrentActorRing(BattleEntity entity)
    {
        if (entity == null || !TryGetLayout())
            return;

        ParticleSystem prefab = entity.IsPlayer ? blueCurrentActorPrefab : enemyCurrentActorPrefab;
        if (prefab == null)
        {
            Debug.LogWarning(
                $"BattleVFXManager：尚未配置{(entity.IsPlayer ? "玩家蓝色" : "敌方")}当前行动者光圈预制体。", this);
            return;
        }

        if (!TryGetWorldPosition(entity, out Vector3 position, out int occupiedCount))
            return;

        List<ParticleSystem> pool = entity.IsPlayer ? _playerRingPool : _enemyRingPool;
        ParticleSystem effect = GetEffect(pool, prefab);

        _activeRing = effect;
        _activeRingEntity = entity;
        // 切换到新的池化实例时丢弃旧的光源引用，下面会重新解析。
        _topLight = null;
        _groundLight = null;
        _topLightCreatedByManager = false;

        ShowEffect(effect, entity, position, prefab, occupiedCount);
    }

    /// <summary>收起当前行动者光圈；取消技能选择、行动结束或进入特写时调用。</summary>
    public void HideCurrentActorRing()
    {
        if (_activeRing == null)
            return;

        HideEffect(_activeRing);
        _activeRing = null;
        _activeRingEntity = null;
        _topLight = null;
        _groundLight = null;
        _topLightCreatedByManager = false;
    }

    /// <summary>角色正式进入行动特写时，隐藏当前行动者光圈。</summary>
    public void HideActionHighlights()
    {
        HideCurrentActorRing();
    }

    private bool TryGetLayout()
    {
        if (_layout != null)
            return true;

        _layout = FindAnyObjectByType<BattleFieldLayout>();
        if (_layout == null)
            Debug.LogWarning("BattleVFXManager：当前未找到 BattleFieldLayout，暂时无法定位战斗光圈。", this);

        return _layout != null;
    }

    private bool TryGetWorldPosition(BattleEntity entity, out Vector3 position, out int occupiedCount)
    {
        position = Vector3.zero;
        occupiedCount = 1;

        if (entity == null || !TryGetLayout())
            return false;

        int logicalPosition = entity.RuntimeData != null ? entity.RuntimeData.Position : 0;
        if (logicalPosition < 1 || logicalPosition > 4)
        {
            Debug.LogWarning($"BattleVFXManager：单位 {entity.ID} 的逻辑站位无效（{logicalPosition}），跳过光圈。", this);
            return false;
        }

        if (entity.IsPlayer)
        {
            position = _layout.GetAllySlotPos(logicalPosition);
        }
        else
        {
            EnemyDfinitionSo enemyDefinition = entity.Definition as EnemyDfinitionSo;
            occupiedCount = enemyDefinition != null
                ? Mathf.Clamp(enemyDefinition.occupiedSlotCount, 1, 4)
                : 1;

            // BattleFieldLayout 已经提供连续占格坐标的平均值，因此大型敌人自动落在占格中心。
            position = _layout.GetEnemySlotPos(logicalPosition, occupiedCount);
        }

        position += worldPositionOffset;
        return true;
    }

    private ParticleSystem GetEffect(List<ParticleSystem> pool, ParticleSystem prefab)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
                return pool[i];
        }

        ParticleSystem instance = Instantiate(prefab, transform);
        instance.gameObject.name = prefab.name + " (Pooled)";
        instance.gameObject.SetActive(false);
        pool.Add(instance);
        return instance;
    }

    private void ShowEffect(ParticleSystem effect, BattleEntity entity, Vector3 position, ParticleSystem prefab, int occupiedCount)
    {
        if (effect == null)
            return;

        effect.transform.position = position;
        // 单槽位单位保持 Prefab 原始尺寸；多占格的大型敌人按占格数量的平方根放大，
        // 避免两格怪的光圈直径直接翻倍、盖住相邻站位。
        float occupiedSlotMultiplier = Mathf.Sqrt(Mathf.Max(1, occupiedCount));
        float finalScale = baseScale * occupiedSlotMultiplier;
        effect.transform.localScale = Vector3.Scale(prefab.transform.localScale, Vector3.one * finalScale);

        if (!effect.gameObject.activeSelf)
            effect.gameObject.SetActive(true);

        effect.Clear(true);
        effect.Play(true);

        EnsureActionLights(effect);
        PlaceActionLights(entity, position);
    }

    /// <summary>
    /// 取（或创建）光圈实例上的两盏点光源。顶光优先复用预制体里已有的 Light，
    /// 地光没有对应预制体组件，统一由管理器创建。
    /// </summary>
    private void EnsureActionLights(ParticleSystem effect)
    {
        if (effect == null)
            return;

        if (_topLight == null && enableActionLight)
        {
            _topLight = effect.GetComponentInChildren<Light>(true);
            if (_topLight == null)
            {
                _topLight = CreatePointLight(effect, "ActionLight", actionLightColor, actionLightIntensity,
                    actionLightRange, actionLightCastShadows);
                _topLightCreatedByManager = true;
            }

            if (_topLight != null)
                ApplyTopLightSettings(_topLight, _topLightCreatedByManager);
        }

        if (enableActionLight && _topLight != null && !_topLight.gameObject.activeSelf)
            _topLight.gameObject.SetActive(true);

        if (_groundLight == null && enableGroundLight)
        {
            Transform existing = effect.transform.Find("ActionGroundLight");
            _groundLight = existing != null
                ? existing.GetComponent<Light>()
                : CreatePointLight(effect, "ActionGroundLight", groundLightColor, groundLightIntensity,
                    groundLightRange, groundLightCastShadows);

            if (_groundLight != null)
                ApplyGroundLightSettings(_groundLight);
        }

        if (enableGroundLight && _groundLight != null && !_groundLight.gameObject.activeSelf)
            _groundLight.gameObject.SetActive(true);
    }

    private void PlaceActionLights(BattleEntity entity, Vector3 groundPosition)
    {
        if (_topLight != null)
            _topLight.transform.position = ResolveActionLightPosition(entity, groundPosition);

        if (_groundLight != null)
            _groundLight.transform.position = ResolveGroundLightPosition(groundPosition);
    }

    private static Light CreatePointLight(
        ParticleSystem effect,
        string objectName,
        Color color,
        float intensity,
        float range,
        bool castShadows)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(effect.transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        return light;
    }

    /// <summary>
    /// 预制体自带光源默认保留作者调好的参数，只接管位置；
    /// 管理器自建光源，或显式开启 overridePrefabLightSettings 时才套用脚本上的参数。
    /// </summary>
    private void ApplyTopLightSettings(Light light, bool createdByManager)
    {
        if (!createdByManager && !overridePrefabLightSettings)
            return;

        light.color = actionLightColor;
        light.intensity = actionLightIntensity;
        light.range = actionLightRange;
        light.shadows = actionLightCastShadows ? LightShadows.Soft : LightShadows.None;
    }

    private void ApplyGroundLightSettings(Light light)
    {
        light.color = groundLightColor;
        light.intensity = groundLightIntensity;
        light.range = groundLightRange;
        light.shadows = groundLightCastShadows ? LightShadows.Soft : LightShadows.None;
    }

    /// <summary>
    /// 顶光世界坐标：
    /// 1. 以角色精灵渲染边界为准，放在边界顶部再加上可调间距，大型敌人会自动更高；
    /// 2. 取不到边界时退回槽位坐标加固定高度；
    /// 3. 最后沿世界 Z 轴朝当前相机方向平移，让光源离开角色所在平面。
    /// </summary>
    private Vector3 ResolveActionLightPosition(BattleEntity entity, Vector3 groundPosition)
    {
        Vector3 anchor = groundPosition + Vector3.up * actionLightFallbackHeight;

        if (entity != null && entity.Unit != null &&
            entity.Unit.TryGetVisualBounds(out Bounds visualBounds))
        {
            anchor = visualBounds.center;
            anchor.y = visualBounds.max.y + actionLightTopGap;
        }

        if (actionLightCameraZOffset <= 0f)
            return anchor;

        Camera camera = Camera.main;
        if (camera == null)
            return anchor;

        // 只在世界 Z 轴上偏移：相机在角色前方还是后方都能自动取到正确方向。
        float directionZ = camera.transform.position.z - anchor.z;
        float sign = directionZ >= 0f ? 1f : -1f;
        anchor.z += sign * actionLightCameraZOffset;
        return anchor;
    }

    /// <summary>地光位置：以光圈落点为基准做垂直偏移，负值即落在特效底下。</summary>
    private Vector3 ResolveGroundLightPosition(Vector3 groundPosition)
    {
        Vector3 position = groundPosition;
        position.y += groundLightOffsetY;
        return position;
    }

    private static void HideEffect(ParticleSystem effect)
    {
        if (effect == null)
            return;

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
    }
}

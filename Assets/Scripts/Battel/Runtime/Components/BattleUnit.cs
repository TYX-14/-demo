using UnityEngine;

/// <summary>
/// 战斗单位的基础类。
/// 负责管理单位在战场的视觉表现，包括核心组件引用、目标选择光标的显隐及位置同步。
/// </summary>
public class BattleUnit : MonoBehaviour
{
    /// <summary>
    /// 当前绑定的角色数据
    /// </summary>
    public BattleEntity Entity {  get; private set; }
    [Header("Base Components")]
    /// <summary>
    /// 单位的动画控制器。
    /// </summary>
    [SerializeField] private Animator animator;

    /// <summary>
    /// 单位的精灵渲染器。
    /// 用于获取单位的物理边界和视觉中心点。
    /// </summary>
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("目标选择光标")]
    /// <summary>
    /// 命中特效/光标的挂载点。
    /// </summary>
    [SerializeField] private Transform hitPoint;

    /// <summary>
    /// 目标选择光标的精灵渲染器。
    /// 用于控制选中状态下的可见性和位置。
    /// </summary>
    [SerializeField] private SpriteRenderer targetCursorRenderer;

    /// <summary>
    /// 小手与单位精灵顶部之间的悬浮距离（世界单位），可在 Inspector 中微调。
    /// </summary>
    [SerializeField, Min(0f)] private float targetCursorGap = 0.15f;

    /// <summary>
    /// 候选目标状态下小手的透明度。悬停时恢复到完全不透明。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float targetCursorCandidateAlpha = 0.55f;

    /// <summary>
    /// 小手沿世界 Z 轴朝镜头方向的偏移，避免与角色精灵图完全重叠。
    /// </summary>
    [SerializeField, Min(0f)] private float targetCursorCameraZOffset = 0.1f;

    /// <summary>
    /// 小手的渲染图层。该图层由 BattleMarkerRenderFeature 在后处理之后重绘，
    /// 因此不会受景深虚化影响。
    /// </summary>
    [SerializeField, Tooltip("小手的渲染图层，需与 BattleMarkerRenderFeature 中配置的图层一致。")]
    private string markerLayerName = "BattleMarker";

    /// <summary>
    /// 小手上下浮动的幅度（世界单位）。设为 0 可关闭浮动。
    /// </summary>
    [SerializeField, Min(0f)] private float targetCursorBobAmplitude = 0.05f;

    /// <summary>
    /// 小手每秒浮动的周期数。
    /// </summary>
    [SerializeField, Min(0f)] private float targetCursorBobFrequency = 1.1f;

    /// <summary>
    /// 小手静止时应处于的本地坐标。浮动只叠加在 Y 轴，不影响命中点。
    /// </summary>
    private Vector3 targetCursorBaseLocalPosition;

    /// <summary>
    /// 小手的显示强度。
    /// 候选目标用半透明常驻标记全部可选中单位，鼠标悬停时才换成完全不透明并浮动。
    /// </summary>
    public enum TargetCursorMode
    {
        Hidden,
        Candidate,
        Hovered
    }

    private TargetCursorMode targetCursorMode = TargetCursorMode.Hidden;
    private Color targetCursorBaseColor = Color.white;

    // 技能攻击特写期间暂存视觉状态，不改动实体的逻辑站位数据。
    private Sprite actionOriginalSprite;
    private Vector3 actionOriginalPosition;
    private Vector3 actionPresentationBasePosition;
    private int actionPresentationStartLogicalPosition;
    private bool actionOriginalAnimatorEnabled;
    private bool actionPresentationActive;

    [Header("合法目标光圈")]
    [SerializeField] private GameObject targetHighlight;
    [SerializeField, Min(0f)] private float targetHighlightGroundOffset = 0.015f;
    [SerializeField, Min(0.1f)] private float targetHighlightWidthMultiplier = 1.2f;
    private SpriteRenderer targetHighlightRenderer;
    private Vector3 targetHighlightBaseScale = Vector3.one;

    [Header("状态图标")]
    /// <summary>
    /// 击退期间显示的眩晕图标（Star）。
    /// </summary>
    [SerializeField] private GameObject star;

    /// <summary>
    /// 组件初始化。
    /// 默认隐藏目标光标，并立即进行一次位置对齐更新。
    /// </summary>
    private void Awake()
    {
        if (targetCursorRenderer != null)
        {
            targetCursorBaseColor = targetCursorRenderer.color;
            targetCursorRenderer.enabled = false;
            ApplyMarkerLayer(targetCursorRenderer.gameObject);
        }
        if (star != null)
            star.SetActive(false);

        InitializeTargetHighlight();
        UpdateTargetCursorPosition();
    }

    private void InitializeTargetHighlight()
    {
        // Prefab 中的光圈是 BattleUnit 的直接子物体，名称来自当前已提交的 prefab。
        if (targetHighlight == null)
        {
            Transform highlightTransform = transform.Find("fire_front_activate_sheet_0");
            if (highlightTransform != null)
                targetHighlight = highlightTransform.gameObject;
        }

        // 防止误把 BattleUnit 根物体或其他外部物体当作光圈关闭。
        if (targetHighlight == gameObject ||
            (targetHighlight != null && !targetHighlight.transform.IsChildOf(transform)))
        {
            Debug.LogWarning("BattleUnit：目标光圈必须是本单位的子物体，已忽略错误引用。", this);
            targetHighlight = null;
            return;
        }

        if (targetHighlight == null)
            return;

        targetHighlightRenderer = targetHighlight.GetComponent<SpriteRenderer>();
        targetHighlightBaseScale = targetHighlight.transform.localScale;
        targetHighlight.SetActive(false);
        UpdateTargetHighlightTransform();
    }

    private void LateUpdate()
    {
        // 精灵动画帧可能改变可见边界，因此光圈持续贴合脚底并调整大小。
        if (targetHighlight != null && targetHighlight.activeSelf)
            UpdateTargetHighlightTransform();

        // 单位在战斗中被位移、或精灵换帧改变边界时，光标需要持续贴合头顶。
        // 这里每帧重算一次基准点，下面的浮动逻辑再在这个基准上叠加偏移。
        if (targetCursorMode != TargetCursorMode.Hidden && targetCursorRenderer != null)
            UpdateTargetCursorPosition();

        // 在位置同步之后叠加轻柔的上下浮动，避免目标选择状态每帧刷新位置时覆盖动画。
        if (targetCursorRenderer != null &&
            targetCursorRenderer.enabled &&
            targetCursorRenderer.gameObject.activeInHierarchy &&
            targetCursorBobAmplitude > 0f &&
            targetCursorBobFrequency > 0f &&
            targetCursorMode == TargetCursorMode.Hovered)
        {
            Vector3 localPosition = targetCursorBaseLocalPosition;
            localPosition.y += Mathf.Sin(Time.time * targetCursorBobFrequency * Mathf.PI * 2f)
                               * targetCursorBobAmplitude;
            targetCursorRenderer.transform.localPosition = localPosition;
        }
    }

    private void UpdateTargetHighlightTransform()
    {
        if (spriteRenderer == null || targetHighlight == null ||
            targetHighlightRenderer == null || spriteRenderer.sprite == null ||
            targetHighlightRenderer.sprite == null)
            return;

        Bounds bounds = spriteRenderer.bounds;
        Vector3 worldPosition = targetHighlight.transform.position;
        worldPosition.x = bounds.center.x;
        worldPosition.y = bounds.min.y + targetHighlightGroundOffset;
        worldPosition.z = bounds.center.z;
        targetHighlight.transform.position = worldPosition;

        float sourceWidth = targetHighlightRenderer.sprite.bounds.size.x *
                            Mathf.Abs(transform.lossyScale.x) *
                            Mathf.Abs(targetHighlightBaseScale.x);
        if (sourceWidth <= 0.0001f)
            return;

        float desiredWidth = Mathf.Max(0.01f, bounds.size.x * targetHighlightWidthMultiplier);
        float scaleRatio = desiredWidth / sourceWidth;
        Vector3 scale = targetHighlightBaseScale;
        scale.x *= scaleRatio;
        scale.y *= scaleRatio;
        targetHighlight.transform.localScale = scale;
    }

    /// <summary>
    /// 显示或隐藏合法目标光圈。光圈对象必须是本单位的子物体。
    /// </summary>
    public void SetTargetHighlight(bool visible)
    {
        if (targetHighlight == null)
            return;

        if (visible)
            UpdateTargetHighlightTransform();

        if (targetHighlight.activeSelf != visible)
            targetHighlight.SetActive(visible);
    }

    /// <summary>
    /// 把小手切到专用标记图层。相机不会直接渲染该图层，
    /// 而是在后处理结束后由 BattleMarkerRenderFeature 重绘，从而绕开景深。
    /// </summary>
    private void ApplyMarkerLayer(GameObject markerObject)
    {
        if (markerObject == null || string.IsNullOrEmpty(markerLayerName))
            return;

        int layer = LayerMask.NameToLayer(markerLayerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                $"BattleUnit：未找到图层「{markerLayerName}」，小手仍会被景深虚化。", this);
            return;
        }

        markerObject.layer = layer;
    }

    /// <summary>
    /// 更新目标光标和命中点的位置。
    /// 计算单位为本地坐标系下的中心Y轴高度，并同步应用到光标和挂载点上。
    /// </summary>
    private void UpdateTargetCursorPosition()
    {
        if (spriteRenderer == null || targetCursorRenderer == null)
            return;

        // 以精灵渲染边界的顶部为基准，而不是使用中心点。
        // 大型敌人的 bounds.max.y 更高，因此小手会自动移动到其头顶上方。
        Bounds bounds = spriteRenderer.bounds;
        Vector3 worldTop = new Vector3(
            bounds.center.x,
            bounds.max.y + targetCursorGap,
            bounds.center.z
        );

        // 沿世界 Z 轴朝镜头方向挪一点，避免小手和角色精灵图完全重叠互相遮挡。
        Camera mainCamera = Camera.main;
        if (mainCamera != null && targetCursorCameraZOffset > 0f)
        {
            float directionZ = mainCamera.transform.position.z - worldTop.z;
            worldTop.z += (directionZ >= 0f ? 1f : -1f) * targetCursorCameraZOffset;
        }

        // 光标和 hitPoint 仍使用单位的本地坐标，保持原有挂点同步方式。
        Vector3 localTop = transform.InverseTransformPoint(worldTop);
        Vector3 localPos = targetCursorRenderer.transform.localPosition;
        localPos.x = localTop.x;
        localPos.y = localTop.y;
        localPos.z = localTop.z;

        targetCursorBaseLocalPosition = localPos;
        targetCursorRenderer.transform.localPosition = targetCursorBaseLocalPosition;
        if (hitPoint != null)
            hitPoint.localPosition = targetCursorBaseLocalPosition;
    }

    /// <summary>
    /// 兼容旧调用：true 等价于悬停态，false 等价于隐藏。
    /// </summary>
    public void SetTargetSelection(bool visible)
    {
        SetTargetCursorMode(visible ? TargetCursorMode.Hovered : TargetCursorMode.Hidden);
    }

    /// <summary>
    /// 设置小手显示强度。
    /// Hidden 关闭；Candidate 半透明且不浮动，用于标出所有可被选中的单位；
    /// Hovered 完全不透明并恢复浮动，用于鼠标悬停。
    /// </summary>
    public void SetTargetCursorMode(TargetCursorMode mode)
    {
        if (targetCursorRenderer == null)
            return;

        targetCursorMode = mode;

        if (mode == TargetCursorMode.Hidden)
        {
            // 目标光标在 Prefab 中可能默认处于 inactive 状态。
            // 仅切换 SpriteRenderer.enabled 无法隐藏 inactive 的 GameObject，因此同步控制两者。
            targetCursorRenderer.enabled = false;
            if (targetCursorRenderer.gameObject.activeSelf)
                targetCursorRenderer.gameObject.SetActive(false);
            return;
        }

        // 两种可见状态都需要贴合角色头顶，避免单位位移后小手留在原地。
        UpdateTargetCursorPosition();

        Color color = targetCursorBaseColor;
        color.a *= mode == TargetCursorMode.Candidate ? targetCursorCandidateAlpha : 1f;
        targetCursorRenderer.color = color;

        targetCursorRenderer.enabled = true;
        if (!targetCursorRenderer.gameObject.activeSelf)
            targetCursorRenderer.gameObject.SetActive(true);

        // 半透明候选态不浮动：一排小手同时上下晃动会抢视线。
        // 这里同时复位上一帧遗留的浮动偏移，避免切回候选态时残留位置。
        targetCursorRenderer.transform.localPosition = targetCursorBaseLocalPosition;
    }

    /// <summary>
    /// 判断屏幕坐标是否落在该单位 SpriteRenderer 投影后的屏幕矩形内。
    /// 用于目标选择，不依赖额外的 2D/3D Collider 配置。
    /// </summary>
    /// <summary>
    /// 开始技能攻击图片演出。仅临时移动视觉单位并替换主 Sprite，不修改战斗阵型数据。
    /// </summary>
    public bool BeginActionPresentation(Sprite attackSprite, Vector3 presentationPosition)
    {
        if (spriteRenderer == null || actionPresentationActive)
            return false;

        actionOriginalSprite = spriteRenderer.sprite;
        actionOriginalPosition = transform.position;
        actionOriginalAnimatorEnabled = animator != null && animator.enabled;
        actionPresentationActive = true;
        actionPresentationStartLogicalPosition =
            Entity != null && Entity.RuntimeData != null ? Entity.RuntimeData.Position : 0;

        if (animator != null)
            animator.enabled = false;

        // attackSprite 允许为空：被攻击者只需要临时移动并冻结当前姿势，不替换图片。
        if (attackSprite != null)
            spriteRenderer.sprite = attackSprite;
        actionPresentationBasePosition = presentationPosition;
        transform.position = presentationPosition;
        return true;
    }

    /// <summary>
    /// 按世界偏移把单位摆到“演出基准位置 + 偏移”。
    /// 只影响视觉 Transform，不改逻辑站位；偏移由演出编排逐帧驱动，特写结束时统一还原。
    /// </summary>
    public void SetActionSlideOffset(Vector3 worldOffset)
    {
        if (!actionPresentationActive)
            return;

        transform.position = actionPresentationBasePosition + worldOffset;
    }

    /// <summary>结束技能攻击图片演出，恢复原始位置、图片和 Animator 状态。</summary>
    public void EndActionPresentation()
    {
        if (!actionPresentationActive)
            return;

        transform.position = actionOriginalPosition;
        if (spriteRenderer != null)
            spriteRenderer.sprite = actionOriginalSprite;
        if (animator != null)
            animator.enabled = actionOriginalAnimatorEnabled;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 特写期间逻辑站位不允许变化：否则这里的还原会覆盖掉那次变更，
        // 表现为角色先弹回原位、再被后置位移挪一次。
        int endLogicalPosition =
            Entity != null && Entity.RuntimeData != null ? Entity.RuntimeData.Position : 0;
        if (endLogicalPosition != actionPresentationStartLogicalPosition)
        {
            Debug.LogWarning(
                $"BattleUnit：特写期间逻辑站位从 {actionPresentationStartLogicalPosition} 变为 {endLogicalPosition}，" +
                "位置还原会覆盖这次变更。", this);
        }
#endif

        actionPresentationActive = false;
    }

    /// <summary>获取角色主精灵的世界空间边界，供攻击特写镜头计算构图。</summary>
    public bool TryGetVisualBounds(out Bounds visualBounds)
    {
        visualBounds = default;
        if (spriteRenderer == null || spriteRenderer.sprite == null ||
            !spriteRenderer.enabled || !spriteRenderer.gameObject.activeInHierarchy)
            return false;

        visualBounds = spriteRenderer.bounds;
        return true;
    }

    public bool TryGetScreenBounds(Camera targetCamera, out Rect screenBounds)
    {
        screenBounds = default;
        if (spriteRenderer == null || targetCamera == null)
            return false;

        Bounds bounds = spriteRenderer.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool foundPointInFrontOfCamera = false;

        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 worldCorner = new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z);

            Vector3 screenCorner = targetCamera.WorldToScreenPoint(worldCorner);
            if (screenCorner.z <= 0f)
                continue;

            foundPointInFrontOfCamera = true;
            minX = Mathf.Min(minX, screenCorner.x);
            minY = Mathf.Min(minY, screenCorner.y);
            maxX = Mathf.Max(maxX, screenCorner.x);
            maxY = Mathf.Max(maxY, screenCorner.y);
        }

        if (!foundPointInFrontOfCamera)
            return false;

        screenBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    /// <summary>
    /// 判断屏幕坐标是否落在该单位 SpriteRenderer 投影后的屏幕矩形内。
    /// </summary>
    public bool ContainsScreenPoint(Vector2 screenPosition, Camera targetCamera)
    {
        return TryGetScreenBounds(targetCamera, out Rect screenBounds) &&
               screenBounds.Contains(screenPosition);
    }

    /// <summary>
    /// 绑定角色数据体
    /// </summary>
    /// <param name="entity"></param>
    public void Bind(BattleEntity entity)
    {
        Entity = entity;
        //拿到动画
        if(entity.Definition.battleAnimator!= null)
            animator.runtimeAnimatorController = entity.Definition.battleAnimator;

        UpdateVisuals();
    }

    /// <summary>
    /// 根据实体状态更新动画控制器的参数，以表现血量和生存状态。
    /// </summary>
    public void UpdateVisuals()
    {
        // 死亡状态处理
        if (!Entity.IsAlive)
        {
            animator.SetBool("isDead", true);
            return;
        }

        // 存活状态更新血量表现
        animator.SetBool("isDead", false);
        float maxHP = Entity.TotalStats.MaxHP;
        float hpRatio = Mathf.Clamp01(Entity.CurrentHP / maxHP);
        animator.SetFloat("hp01", hpRatio);
    }

    /// <summary>同步显示运行时眩晕状态；与击退动画的临时图标共用 Star。</summary>
    public void RefreshStunVisual()
    {
        if (star != null)
            star.SetActive(Entity != null && Entity.RuntimeData != null &&
                           Entity.RuntimeData.StunActionsRemaining > 0);
    }

    /// <summary>
    /// 主动移动协程：平滑移动到目标位置，并播放现有的移动动画。
    /// </summary>
    public IEnumerator MoveToPosition(Vector3 targetPos, float duration = 0.5f)
    {
        animator.SetBool("isMoving", true);

        Vector3 startPos = transform.position;
        float elapsedTime = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        animator.SetBool("isMoving", false);
    }

    /// <summary>
    /// 技能前进/后退专用的迅猛平移：不触发走路动画，不使用缓动或到达后的阻尼。
    /// 以固定短时长线性抵达目标点，结束时立即停稳。
    /// </summary>
    public IEnumerator FastMoveToPosition(Vector3 targetPos, float duration = 0.12f)
    {
        if (animator != null)
            animator.SetBool("isMoving", false);

        Vector3 startPos = transform.position;
        duration = Mathf.Max(0.01f, duration);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = Mathf.Clamp01(elapsedTime / duration);
            transform.position = Vector3.LerpUnclamped(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        if (animator != null)
            animator.SetBool("isMoving", false);
    }

    /// <summary>
    /// 击退位移：快速抵达目标点，再沿击退方向做短暂衰减振荡。
    /// 全程不触发跑步动画，结束时精确停在目标站位。
    /// </summary>
    public IEnumerator ForcedMoveToPosition(
        Vector3 targetPos,
        float duration = 0.15f,
        float shakeAmplitude = 0.12f,
        float shakeDuration = 0.35f,
        float shakeFrequency = 32f)
    {
        animator.SetBool("isMoving", false);
        if (star != null)
            star.SetActive(true);

        Vector3 startPos = transform.position;
        Vector3 knockbackDirection = targetPos - startPos;
        if (knockbackDirection.sqrMagnitude > 0.0001f)
            knockbackDirection.Normalize();

        float elapsedTime = 0f;
        duration = Mathf.Max(0.01f, duration);

        // 击退阶段比正常补位更快，不播放跑步动画。
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        // 到达后沿击退方向短暂震荡，振幅随时间衰减，避免角色一直抖动。
        elapsedTime = 0f;
        shakeDuration = Mathf.Max(0.01f, shakeDuration);
        while (elapsedTime < shakeDuration)
        {
            float t = elapsedTime / shakeDuration;
            float damping = 1f - t;
            float offset = Mathf.Sin(elapsedTime * shakeFrequency) * shakeAmplitude * damping;
            transform.position = targetPos + knockbackDirection * offset;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        // 击退位移与衰减震荡全部结束后，关闭眩晕图标。
        RefreshStunVisual();
    }

}
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
        targetCursorRenderer.enabled = false;
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

        // 在位置同步之后叠加轻柔的上下浮动，避免目标选择状态每帧刷新位置时覆盖动画。
        if (targetCursorRenderer != null &&
            targetCursorRenderer.enabled &&
            targetCursorRenderer.gameObject.activeInHierarchy &&
            targetCursorBobAmplitude > 0f &&
            targetCursorBobFrequency > 0f)
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
    /// 设置目标选择光标的显隐状态。
    /// </summary>
    /// <param name="visible">True 表示显示并刷新位置，False 表示隐藏。</param>
    public void SetTargetSelection(bool visible)
    {
        if (targetCursorRenderer == null)
            return;

        if (visible)
            UpdateTargetCursorPosition();

        // 目标光标在 Prefab 中可能默认处于 inactive 状态。
        // 仅切换 SpriteRenderer.enabled 无法显示 inactive 的 GameObject，因此同步控制两者。
        targetCursorRenderer.enabled = visible;
        if (targetCursorRenderer.gameObject.activeSelf != visible)
            targetCursorRenderer.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 判断屏幕坐标是否落在该单位 SpriteRenderer 投影后的屏幕矩形内。
    /// 用于目标选择，不依赖额外的 2D/3D Collider 配置。
    /// </summary>
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
        if (star != null)
            star.SetActive(false);
    }

}
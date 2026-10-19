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

        UpdateTargetCursorPosition();
    }

    /// <summary>
    /// 更新目标光标和命中点的位置。
    /// 计算单位为本地坐标系下的中心Y轴高度，并同步应用到光标和挂载点上。
    /// </summary>
    private void UpdateTargetCursorPosition()
    {
        // 1. 获取 Sprite 边界的全局世界中心点
        Vector3 worldCenter = spriteRenderer.bounds.center;

        // 2. 将世界中心点转换为当前单位的局部坐标
        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

        // 3. 读取光标当前的局部坐标（保留X轴和Z轴原始数据）
        Vector3 localPos = targetCursorRenderer.transform.localPosition;

        // 4. 仅更新Y轴高度，使其与单位中心对齐
        localPos.y = localCenter.y;

        // 5. 将坐标应用到目标光标和命中点
        targetCursorRenderer.transform.localPosition = localPos;
        hitPoint.localPosition = localPos;
    }

    /// <summary>
    /// 设置目标选择光标的显隐状态。
    /// </summary>
    /// <param name="visible">True 表示显示并刷新位置，False 表示隐藏。</param>
    public void SetTargetSelection(bool visible)
    {
        if (visible)
        {
            UpdateTargetCursorPosition();
        }
        targetCursorRenderer.enabled = visible;
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
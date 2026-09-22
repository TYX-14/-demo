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
    /// 组件初始化。
    /// 默认隐藏目标光标，并立即进行一次位置对齐更新。
    /// </summary>
    private void Awake()
    {
        targetCursorRenderer.enabled = false;
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
    /// 移动协程：让单位平滑移动到目标位置。
    /// </summary>
    /// <param name="targetPos">目标位置。</param>
    /// <param name="duration">移动耗时（默认0.5秒）。</param>
    public IEnumerator MoveToPosition(Vector3 targetPos, float duration = 0.5f)
    {
        // 开始移动前，开启移动动画状态
        animator.SetBool("isMoving", true);

        Vector3 startPos = transform.position;
        float elapsedTime = 0f;

        // 在持续时间内，逐帧进行插值移动
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration; // 计算插值进度 t (0~1)
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;    // 累加已经过的时间
            yield return null;                // 等待下一帧继续循环
        }

        // 循环结束后，强制对齐到目标点（防止浮点运算造成的微小误差）
        transform.position = targetPos;
        // 移动完成，关闭移动动画状态
        animator.SetBool("isMoving", false);
    }

}
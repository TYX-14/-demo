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
    /// 强制位移协程：平滑移动到目标位置，但不触发跑步动画。
    /// 可用于击退等受迫移动。受击/晕眩动画由角色自己的 Animator 配置负责。
    /// </summary>
    public IEnumerator ForcedMoveToPosition(Vector3 targetPos, float duration = 0.25f)
    {
        // 确保强制位移期间不会因为 isMoving 而进入跑步状态。
        animator.SetBool("isMoving", false);

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
    }

}
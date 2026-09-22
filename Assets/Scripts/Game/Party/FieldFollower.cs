/// <summary>
/// 控制跟随者的动画机，控制移动逻辑(单个跟随者本体)
/// </summary>
public class FieldFollower : MonoBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;

    [Header("动画机参数")]
    [SerializeField] private string isMovingParam = "isMoving";
    [SerializeField] private string moveXParam = "moveX";
    [SerializeField] private string moveYParam = "moveY";

    [Header("最小位移阈值")]
    [SerializeField] private float movementThreshold = 0.001f;

    [Header("重力")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundSnapFroce = -2f;
    [SerializeField] private float maxVelocity = -40f;
    private float verticalVelocity;

    #region 对外接口
    /// <summary>
    /// 设置跟随者的动画机
    /// </summary>
    public void SetupFollower(CharacterDefinitionSO definition)
    {
        
        animator.runtimeAnimatorController = definition.filedAnimator;
    }

    public void MoveTo(Vector3 targetPosition,float speed)
    {   
        //计算到目标的向量
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        //限制步长
        Vector3 HorizontalStep = Vector3.ClampMagnitude(toTarget, Mathf.Max(0, speed) * Time.deltaTime);

        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            //设置垂直速度为地面吸附力，让角色稳定在地面上
            verticalVelocity = groundSnapFroce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < maxVelocity)
            {
                verticalVelocity = maxVelocity;
            }
        }

        Vector3 movement = HorizontalStep;
        movement.y = verticalVelocity*Time.deltaTime;

        characterController.Move(movement);
        UpdateAnimation(HorizontalStep);
    }

    #endregion
    /// <summary>
    /// 设置动画机参数
    /// </summary>
    /// <param name="step"></param>
    private void UpdateAnimation(Vector3 step)
    {
        //判断步长的模长是否大于死区
        bool isMoving = step.magnitude >movementThreshold*movementThreshold;
        animator.SetBool(isMovingParam, isMoving);

        if (isMoving)
        {
            animator.SetFloat (moveXParam, step.x);
            animator.SetFloat (moveYParam, step.z);
        }
    }
    /// <summary>
    /// 贴住
    /// </summary>
    public void SnapTo(Vector3 position)
    {
        bool enabled = characterController.enabled;
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = enabled;

        verticalVelocity = 0f;
    }
}

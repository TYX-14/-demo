using UnityEngine.InputSystem.Controls;

/// <summary>
/// 实现人物的移动，动画效果
/// </summary>
public class PlayerController : MonoBehaviour
{
    private CharacterController characterController;
    private Animator animator;
    [SerializeField] private float speed = 5f;
    private bool isMoving;


    [Header("重力")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundSnapFroce = -2f;
    [SerializeField] private float maxVelocity = -40f;


    private float verticalVelocity;
    private Vector2 movementInpunt;
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }
    private void Update()
    {
        Movement();
        SetAnimaton();
    }

    private void Movement()
    {
        var input = InputSystemController.Instance;
        if (input == null) return;//防止update速度太快，导致单列模式还没有创建

        movementInpunt = input.GetMovementInput();

        bool  isGrounded =characterController.isGrounded;

        if (isGrounded && verticalVelocity<0)
        {
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

        Vector3 velocity = new(movementInpunt.x*speed, 0, movementInpunt.y*speed);
        velocity.y = verticalVelocity;

        characterController.Move(velocity*Time.deltaTime);
        
    }

    private void SetAnimaton()
    {
        if (animator == null) return;
        isMoving = movementInpunt.magnitude > 0.1f;
        animator.SetBool("isMoving", isMoving);
        if (isMoving)
        {
            animator.SetFloat("moveX", movementInpunt.x);
            animator.SetFloat("moveY", movementInpunt.y);
        }
    }
}

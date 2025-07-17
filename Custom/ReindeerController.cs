using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class ReindeerController : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("걷는 속도")]
    public float walkSpeed = 3f;
    [Tooltip("달리는 속도")]
    public float runSpeed = 6f;
    [Tooltip("점프 힘")]
    public float jumpForce = 8f;
    [Tooltip("캐릭터 회전 속도")]
    public float turnSpeed = 10f;

    [Header("중력 조정")]
    [Tooltip("점프 상승 시 중력 계수 (기본 중력보다 얼마나 더 적용할지)")]
    public float gravityMultiplier = 2.5f;
    [Tooltip("낙하 시 중력 계수 (기본 중력보다 얼마나 더 적용할지)")]
    public float fallMultiplier = 5f;

    [Header("지면 체크")]
    [Tooltip("캐릭터 발 아래에서 지면을 감지할 구체의 반지름 (콜라이더 반지름의 1/3 ~ 1/2 권장)")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("지면 체크 구체의 중심점 오프셋 (캐릭터 피벗에서 발까지의 대략적인 거리)")]
    public float groundCheckOffset = 0.1f;
    [Tooltip("지면으로 인식할 레이어")]
    public LayerMask groundMask;

    [Header("Idle 설정")]
    [Tooltip("특별한 Idle 애니메이션을 재생하기까지 기다리는 최소 시간")]
    public float minIdleWaitTime = 5f;
    [Tooltip("특별한 Idle 애니메이션을 재생하기까지 기다리는 최대 시간")]
    public float maxIdleWaitTime = 10f;

    [Header("점프 쿨타임")]
    [Tooltip("점프 후 다음 점프가 가능하기까지의 시간 (초)")]
    public float jumpCooldownTime = 1f;

    [Header("애니메이션 설정")]
    [Tooltip("애니메이터 속도 보간을 위한 임계값")]
    public float animatorSpeedThreshold = 0.1f;
    [Tooltip("걷기 속도 애니메이션 값")]
    public float animatorWalkSpeed = 1f;
    [Tooltip("달리기 속도 애니메이션 값")]
    public float animatorRunSpeed = 2f;

    [Header("참조")]
    [Tooltip("카메라 Transform. 할당하지 않으면 Main Camera를 사용합니다.")]
    public Transform cameraTransform;

    // --- 비공개 변수 ---
    private Rigidbody rb;
    private Animator animator;
    private Vector3 moveInput;
    private bool isRunning;
    private bool isGrounded;
    private bool isDead;
    private bool hasCompletionBeenHandled = false;
    private int currentStateHash;
    private int previousStateHash;
    private float nextJumpTime;

    // --- Idle Break 관련 변수 ---
    private float idleTimer;
    private float nextIdleTime;

    // --- 애니메이터 파라미터 해시 (성능 최적화) ---
    private static readonly int hashSpeed = Animator.StringToHash("Speed");
    private static readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int hashJump = Animator.StringToHash("Jump");
    private static readonly int hashAttack = Animator.StringToHash("Attack");
    private static readonly int hashDie = Animator.StringToHash("Die");
    private static readonly int hashIdleBreak = Animator.StringToHash("IdleBreak");
    private static readonly int hashIsEating = Animator.StringToHash("IsEating");
    private static readonly int hashEatTrigger = Animator.StringToHash("EatTrigger");

    // --- 애니메이터 상태 해시 ---
    private static readonly int stateHashIdle = Animator.StringToHash("Idle");
    private static readonly int stateHashEatingIn = Animator.StringToHash("Eating_In");
    private static readonly int stateHashEatingLoop = Animator.StringToHash("Eating_Loop");
    private static readonly int stateHashEatingOut = Animator.StringToHash("Eating_Out");

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        rb.freezeRotation = true;

        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("ReindeerController: Main Camera not found or not tagged as 'MainCamera'. Please assign cameraTransform manually.");
            }
        }

        SetNewIdleTime();
        nextJumpTime = Time.time; // 게임 시작 시 바로 점프 가능하도록 초기화
    }

    void Update()
    {
        GroundCheck();
        HandleInput();
        HandleIdleBreaks();

        // 매 프레임 애니메이터 상태 해시 업데이트
        previousStateHash = currentStateHash;
        currentStateHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        // 상태 변화 감지 및 isEating 초기화
        CheckForEatingCompletion();
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyBetterGravity();
        UpdateAnimator();
    }

    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(h, 0f, v).normalized;
        isRunning = Input.GetKey(KeyCode.LeftShift);

        if (isDead) return;

        bool isEating = animator.GetBool(hashIsEating);

        // 점프 입력 (쿨타임 조건 추가)
        if (isGrounded && Input.GetButtonDown("Jump") && Time.time >= nextJumpTime)
        {
            if (isEating)
            {
                CancelEating();
                Debug.Log("Eating cancelled due to Jump.");
            }

            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger(hashJump);
            nextJumpTime = Time.time + jumpCooldownTime; // 점프 성공 시 쿨타임 설정
        }

        // E 키로 Eating 시작
        if (Input.GetKeyDown(KeyCode.E) && isGrounded && moveInput.magnitude < animatorSpeedThreshold)
        {
            if (!isEating)
            {
                animator.SetTrigger(hashEatTrigger);
                animator.SetBool(hashIsEating, true);
                hasCompletionBeenHandled = false;
                Debug.Log("Attempting to start Eating with Trigger.");
            }
        }

        // 움직이면 Eating 취소
        if (isEating && moveInput.magnitude > animatorSpeedThreshold)
        {
            CancelEating();
            Debug.Log("Eating cancelled due to movement.");
        }

        if (Input.GetMouseButtonDown(0)) animator.SetTrigger(hashAttack);
        if (Input.GetKeyDown(KeyCode.K))
        {
            animator.SetTrigger(hashDie);
            isDead = true;
            this.enabled = false;
        }
    }

    private void CancelEating()
    {
        animator.SetBool(hashIsEating, false);
        hasCompletionBeenHandled = false;
    }

    private void CheckForEatingCompletion()
    {
        if (hasCompletionBeenHandled)
        {
            return;
        }

        // 이전 상태가 Eating_Out이었고 현재 상태가 Idle로 전환되었을 때
        if (previousStateHash == stateHashEatingOut && currentStateHash == stateHashIdle)
        {
            CancelEating();
            hasCompletionBeenHandled = true;
            Debug.Log("✅ State transition detected: Eating_Out -> Idle. isEating is now false.");
        }
    }

    private void HandleIdleBreaks()
    {
        bool isEating = animator.GetBool(hashIsEating);
        if (moveInput.magnitude < animatorSpeedThreshold && isGrounded && !isEating)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= nextIdleTime)
            {
                animator.SetTrigger(hashIdleBreak);
                idleTimer = 0f;
                SetNewIdleTime();
            }
        }
        else
        {
            idleTimer = 0f;
        }
    }

    private void SetNewIdleTime()
    {
        nextIdleTime = Random.Range(minIdleWaitTime, maxIdleWaitTime);
    }

    private void ApplyMovement()
    {
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 currentVelocity = rb.velocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        if (moveInput.magnitude >= animatorSpeedThreshold)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 desiredMoveDirection = (camForward * moveInput.z + camRight * moveInput.x).normalized;

            if (desiredMoveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            }

            Vector3 newHorizontalVelocity = Vector3.Lerp(horizontalVelocity, desiredMoveDirection * targetSpeed, Time.fixedDeltaTime * 10f);

            rb.velocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);
        }
        else
        {
            rb.velocity = new Vector3(Mathf.Lerp(currentVelocity.x, 0, Time.fixedDeltaTime * 10f),
                                     currentVelocity.y,
                                     Mathf.Lerp(currentVelocity.z, 0, Time.fixedDeltaTime * 10f));
        }
    }

    private void ApplyBetterGravity()
    {
        if (rb.velocity.y > 0)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.velocity.y < 0)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void GroundCheck()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(transform.position + Vector3.up * groundCheckOffset, groundCheckDistance, groundMask);

        if (!wasGrounded && isGrounded)
        {
            Debug.Log("Character Landed! Is Grounded: " + isGrounded);
        }
        else if (wasGrounded && !isGrounded)
        {
            Debug.Log("Character Left the Ground!");
        }

        Debug.Log("Is Grounded: " + isGrounded);
    }

    private void UpdateAnimator()
    {
        float speedValue = 0f;
        if (moveInput.magnitude > animatorSpeedThreshold)
        {
            speedValue = isRunning ? animatorRunSpeed : animatorWalkSpeed;
        }

        animator.SetFloat(hashSpeed, speedValue, 0.1f, Time.fixedDeltaTime);

        if (animator.GetFloat(hashSpeed) < 0.01f)
        {
            animator.SetFloat(hashSpeed, 0f);
        }

        animator.SetBool(hashIsGrounded, isGrounded);
    }

    void OnDrawGizmosSelected()
    {
        if (rb == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * groundCheckOffset, groundCheckDistance);
        Gizmos.color = Color.blue;
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (horizontalVelocity.magnitude > 0.1f)
        {
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, horizontalVelocity.normalized * 1f);
        }
    }
}
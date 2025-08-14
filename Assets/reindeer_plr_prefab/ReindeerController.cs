using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class ReindeerController_Improved : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("걷는 속도")]
    public float walkSpeed = 2.5f;
    [Tooltip("달리는 속도")]
    public float runSpeed = 5.6f;
    [Tooltip("캐릭터 회전 속도")]
    public float turnSpeed = 8f;
    [Tooltip("수평 이동 시 가속/감속 보간 시간 (낮을수록 빠르게 반응)")]
    public float moveSmoothTime = 0.1f;

    [Header("점프 설정")]
    [Tooltip("점프 힘")]
    public float jumpForce = 8f;
    [Tooltip("점프 후 다음 점프가 가능하기까지의 시간 (초)")]
    public float jumpCooldownTime = 0.5f;
    [Tooltip("땅에 닿지 않아도 짧은 시간 동안 점프가 가능한 시간 (코요테 타임)")]
    public float coyoteTime = 0.15f;
    [Tooltip("점프 버튼을 미리 눌러도 저장되는 시간 (점프 버퍼)")]
    public float jumpBufferTime = 0.2f;

    [Header("중력 조정")]
    [Tooltip("점프 상승 시 중력 계수 (기본 중력보다 얼마나 더 적용할지)")]
    public float gravityMultiplier = 2.5f;
    [Tooltip("낙하 시 중력 계수 (기본 중력보다 얼마나 더 적용할지)")]
    public float fallMultiplier = 5f;

    [Header("지면 체크")]
    [Tooltip("캐릭터 발 아래에서 지면을 감지할 구체의 반지름 (콜라이더 반지름의 1/3 ~ 1/2 권장)")]
    public float groundCheckDistance = 0.2f;
    [Tooltip("지면 체크 구체의 중심점 오프셋 (캐릭터 피벗에서 발까지의 대략적인 거리)")]
    public float groundCheckOffset = 0.1f;
    [Tooltip("지면으로 인식할 레이어")]
    public LayerMask groundMask;

    [Header("Stop/Idle 설정")]
    [Tooltip("Stop 상태에서 Idle 애니메이션 발동까지 기다리는 최소 시간 (랜덤)")]
    public float minTimeToIdleFromStop = 5f;
    [Tooltip("Stop 상태에서 Idle 애니메이션 발동까지 기다리는 최대 시간 (랜덤)")]
    public float maxTimeToIdleFromStop = 7f;

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
    private bool _isGrounded; // 스크립트 내부에서 사용하는 지면 여부
    private bool isDead;
    private int currentStateHash;
    private int previousStateHash;

    // 점프 관련 타이머
    private float lastGroundedTime;
    private float jumpBufferTimer;
    private float lastJumpTime;

    // 수평 이동 속도 보간을 위한 변수
    private Vector3 currentHorizontalVelocity;
    private Vector3 smoothDampVelocity;

    // --- Idle 관련 타이머 ---
    private float timeInStopState; // Stop 상태에 머무는 시간
    private float nextIdleTriggerDelay; // 다음 IdleTrigger 발동까지의 지연 시간

    // --- 애니메이터 파라미터 해시 (성능 최적화) ---
    private static readonly int hashSpeed = Animator.StringToHash("Speed");
    private static readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int hashJump = Animator.StringToHash("Jump");
    private static readonly int hashAttack = Animator.StringToHash("Attack");
    private static readonly int hashDie = Animator.StringToHash("Die");
    private static readonly int hashIsEating = Animator.StringToHash("IsEating");
    private static readonly int hashEatTrigger = Animator.StringToHash("EatTrigger");
    private static readonly int hashIdleTrigger = Animator.StringToHash("IdleTrigger");

    // --- 애니메이터 상태 해시 ---
    private static readonly int stateHashIdle = Animator.StringToHash("Idle");
    private static readonly int stateHashEatingIn = Animator.StringToHash("Eating_In");
    private static readonly int stateHashEatingLoop = Animator.StringToHash("Eating_Loop");
    private static readonly int stateHashEatingOut = Animator.StringToHash("Eating_Out");
    private static readonly int stateHashJump = Animator.StringToHash("Jump");
    private static readonly int stateHashFall = Animator.StringToHash("Fall");
    private static readonly int stateHashStop = Animator.StringToHash("Stop");
    private static readonly int stateHashWalkRunBlend = Animator.StringToHash("WalkRunBlend");


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (rb == null || animator == null)
        {
            Debug.LogError("Rigidbody 또는 Animator 컴포넌트를 찾을 수 없습니다. RequireComponent 설정을 확인하세요.");
            enabled = false;
            return;
        }

        rb.freezeRotation = true;

        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("Main Camera가 없거나 'MainCamera' 태그가 지정되지 않았습니다. cameraTransform을 수동으로 할당해주세요.");
            }
        }

        SetNextIdleTriggerDelay(); // 초기 IdleTrigger 지연 시간 설정
        timeInStopState = 0f;
        lastGroundedTime = Time.time;
        lastJumpTime = Time.time - jumpCooldownTime;
    }

    void Update()
    {
        HandleInput();

        previousStateHash = currentStateHash;
        currentStateHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        CheckForEatingCompletion();
        HandleStopAndIdleLoop();

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        // 디버그 로그 (옵션, 필요시 주석 해제하여 사용)
        // Debug.Log($"[DEBUG] Update: moveInput.magnitude: {moveInput.magnitude:F3}, Current Animator Speed: {animator.GetFloat(hashSpeed):F3}, Current State: {animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, TimeInStopState: {timeInStopState:F3}, NextIdleTriggerDelay: {nextIdleTriggerDelay:F3}, _isGrounded: {_isGrounded}");
    }

    void FixedUpdate()
    {
        GroundCheck();
        ApplyMovement();
        ApplyBetterGravity();
        UpdateAnimator();
    }

    private void HandleStopAndIdleLoop()
    {
        bool isMoving = moveInput.magnitude >= animatorSpeedThreshold;
        bool isEating = animator.GetBool(hashIsEating);

        // Stop 상태일 때만 타이머를 증가시킴 (움직이지 않고, 땅에 있고, 먹는 중이 아니고, 죽지 않았을 때)
        if (currentStateHash == stateHashStop && !isMoving && _isGrounded && !isEating && !isDead)
        {
            timeInStopState += Time.deltaTime;
            if (timeInStopState >= nextIdleTriggerDelay)
            {
                animator.SetTrigger(hashIdleTrigger);
                timeInStopState = 0f; // 타이머 리셋
                SetNextIdleTriggerDelay(); // 다음 IdleTrigger 발동까지의 새로운 랜덤 시간 설정
            }
        }
        else
        {
            // Stop 상태가 아니거나, 움직이거나, 공중에 있거나, 다른 행동 중일 때는 타이머 리셋
            timeInStopState = 0f;
        }
    }

    private void SetNextIdleTriggerDelay()
    {
        nextIdleTriggerDelay = Random.Range(minTimeToIdleFromStop, maxTimeToIdleFromStop);
    }

    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(h, 0f, v).normalized;
        isRunning = Input.GetKey(KeyCode.LeftShift);

        if (isDead) return;

        bool isEating = animator.GetBool(hashIsEating);

        bool canJumpFromGroundOrCoyote = (_isGrounded || Time.time - lastGroundedTime <= coyoteTime);
        bool canJumpFromCooldown = (Time.time - lastJumpTime >= jumpCooldownTime);

        bool shouldAttemptJump = canJumpFromGroundOrCoyote && canJumpFromCooldown && (jumpBufferTimer > 0f || Input.GetButtonDown("Jump"));

        if (shouldAttemptJump)
        {
            PerformJump(isEating);
        }

        if (Input.GetKeyDown(KeyCode.E) && _isGrounded && moveInput.magnitude < animatorSpeedThreshold)
        {
            if (!isEating)
            {
                animator.SetTrigger(hashEatTrigger);
                animator.SetBool(hashIsEating, true);
            }
        }

        if (isEating && moveInput.magnitude > animatorSpeedThreshold)
        {
            CancelEating();
        }

        if (Input.GetMouseButtonDown(0)) animator.SetTrigger(hashAttack);
        if (Input.GetKeyDown(KeyCode.K))
        {
            animator.SetTrigger(hashDie);
            isDead = true;
            this.enabled = false;
        }
    }

    private void PerformJump(bool isEating)
    {
        if (isEating)
        {
            animator.ResetTrigger(hashEatTrigger);
            animator.SetBool(hashIsEating, false);
        }

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // ★★★ 점프 시작 시 isGrounded를 강제로 false로 설정하여 애니메이터의 점프 상태 진입을 보장
        _isGrounded = false;
        animator.SetBool(hashIsGrounded, false); // 애니메이터 파라미터도 즉시 업데이트

        animator.SetTrigger(hashJump);
        jumpBufferTimer = 0f;
        lastJumpTime = Time.time;
    }

    private void CancelEating()
    {
        animator.SetBool(hashIsEating, false);
        animator.ResetTrigger(hashEatTrigger);
    }

    private void CheckForEatingCompletion()
    {
        if (previousStateHash == stateHashEatingOut && currentStateHash == stateHashStop)
        {
            animator.SetBool(hashIsEating, false);
        }
    }

    private void ApplyMovement()
    {
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 currentVelocity = rb.velocity;

        float currentMoveSmoothTime = moveSmoothTime;
        if (!_isGrounded && rb.velocity.y < 0)
        {
            currentMoveSmoothTime *= 2f;
        }

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

            Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed;
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, targetHorizontalVelocity, ref smoothDampVelocity, currentMoveSmoothTime);

            rb.velocity = new Vector3(currentHorizontalVelocity.x, currentVelocity.y, currentHorizontalVelocity.z);
        }
        else
        {
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, currentMoveSmoothTime);
            rb.velocity = new Vector3(currentHorizontalVelocity.x, currentVelocity.y, currentVelocity.z);
        }
    }

    private void ApplyBetterGravity()
    {
        if (rb.velocity.y > 0 && !animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"))
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
        bool wasGrounded = _isGrounded;
        CapsuleCollider capCol = GetComponent<CapsuleCollider>();

        if (capCol == null)
        {
            _isGrounded = false;
            return;
        }

        Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + groundCheckOffset);

        _isGrounded = Physics.CheckSphere(sphereOrigin, groundCheckDistance, groundMask);

        if (_isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        // 착지 시 미세한 y 속도 조정
        if (_isGrounded && !wasGrounded && rb.velocity.y < 0f)
        {
            if (Mathf.Abs(rb.velocity.y) < 0.2f)
            {
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            }
        }

        // ★ 중요: 이제 _isGrounded 값 업데이트는 GroundCheck()에서 하고,
        // animator.SetBool(hashIsGrounded, _isGrounded) 호출은 UpdateAnimator()에서만 합니다.
        // PerformJump()에서 강제로 false로 설정하는 로직이 우선권을 가집니다.
    }

    private void UpdateAnimator()
    {
        float speedValue = 0f;
        if (moveInput.magnitude > animatorSpeedThreshold)
        {
            speedValue = isRunning ? animatorRunSpeed : animatorWalkSpeed;
        }

        animator.SetFloat(hashSpeed, speedValue, 0.1f, Time.fixedDeltaTime);
        // ★ GroundCheck()에서 계산된 _isGrounded 값을 Animator에 반영합니다.
        // Jump 시 PerformJump()에서 이미 false로 설정했으므로, 여기서 다시 true로 덮어쓰지 않습니다.
        animator.SetBool(hashIsGrounded, _isGrounded);
    }

    void OnDrawGizmosSelected()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        Gizmos.color = Color.green;
        CapsuleCollider capCol = GetComponent<CapsuleCollider>();
        if (capCol != null)
        {
            Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + groundCheckOffset);
            Gizmos.DrawWireSphere(sphereOrigin, groundCheckDistance);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + Vector3.up * groundCheckOffset, groundCheckDistance);
        }

        Gizmos.color = Color.blue;
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (horizontalVelocity.magnitude > 0.1f)
        {
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, horizontalVelocity.normalized * 1f);
        }
    }
}
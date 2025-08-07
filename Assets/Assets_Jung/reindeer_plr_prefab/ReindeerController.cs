using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody), typeof(Animator), typeof(CapsuleCollider))]
public class ReindeerController : MonoBehaviour
{
    // --- 설정 변수들 ---
    [Header("이동 설정")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.6f;
    public float turnSpeed = 8f;
    public float moveSmoothTime = 0.1f;

    [Header("점프 설정")]
    public float jumpForce = 350f;
    public float jumpCooldownTime = 1f;
    public float coyoteTime = 0.15f; // 지면에서 떨어진 후에도 잠시 점프 가능한 시간
    public float jumpBufferTime = 0.2f; // 점프 입력을 미리 받아두는 시간

    [Header("중력 조정")]
    public float gravityMultiplier = 2.5f; // 일반 중력 배율
    public float fallMultiplier = 5f; // 하강 시 중력 배율 (더 빠르게 떨어지도록)

    [Header("지면 체크")]
    public float groundCheckDistance = 0.2f;
    public float groundCheckOffset = 0.1f;
    public LayerMask groundMask;

    [Header("참조")]
    public Transform cameraTransform;
    public bool IsMoving => moveInputVec2.magnitude > 0.1f;

    [Header("상호작용 설정")]
    [Tooltip("상호작용을 위해 꾹 눌러야 하는 시간")]
    public float interactionHoldDuration = 3f;

    // --- 비공개 변수 ---
    private Rigidbody rb;
    private Animator animator;
    private Reindeer_Input inputActions;

    private Vector2 moveInputVec2;
    private bool isRunning;
    private bool _isGrounded; // 현재 지면에 닿아있는지 여부
    private bool isInteracting; // 상호작용 중인지 여부

    private float lastGroundedTime; // 마지막으로 지면에 닿았던 시간
    private float jumpBufferTimer; // 점프 버퍼 타이머
    private float lastJumpTime; // 마지막 점프 시간

    private Vector3 currentHorizontalVelocity;
    private Vector3 smoothDampVelocity;

    // 상호작용 관련 변수
    private InteractableBox currentInteractableBox;
    private Coroutine interactionCoroutine;

    // --- 애니메이터 해시 ---
    private static readonly int hashSpeed = Animator.StringToHash("Speed");
    private static readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int hashJump = Animator.StringToHash("Jump");
    private static readonly int hashIsEating = Animator.StringToHash("IsEating");
    private static readonly int hashEatTrigger = Animator.StringToHash("EatTrigger");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.freezeRotation = true;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        inputActions = new Reindeer_Input();
        SetupInputCallbacks();
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    void Update()
    {
        HandleTimers(); // 점프 버퍼 및 코요테 타임 로직을 위해 다시 호출
        UpdateAnimator();

        // 상호작용 중 이동이 감지되면 상호작용 취소
        if (isInteracting && moveInputVec2.magnitude > 0.1f)
        {
            CancelInteraction();
        }
    }

    void FixedUpdate()
    {
        GroundCheck();
        ApplyMovement();
        ApplyBetterGravity();
    }

    private void SetupInputCallbacks()
    {
        inputActions.Player.Move.performed += ctx => moveInputVec2 = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInputVec2 = Vector2.zero;

        inputActions.Player.Run.performed += ctx => isRunning = true;
        inputActions.Player.Run.canceled += ctx => isRunning = false;

        // 점프 입력 시 jumpBufferTimer를 설정하여 점프 버퍼 로직을 사용
        inputActions.Player.Jump.performed += ctx => jumpBufferTimer = jumpBufferTime;

        // 상호작용 (F 키)
        inputActions.Player.Interact.started += ctx => StartInteraction();
        inputActions.Player.Interact.canceled += ctx => CancelInteraction();
    }

    private void HandleTimers()
    {
        jumpBufferTimer -= Time.deltaTime; // 점프 버퍼 타이머 감소

        // 지면에 있거나 코요테 타임 내에 있을 때 점프 가능
        bool canJumpFromGroundOrCoyote = (_isGrounded || Time.time - lastGroundedTime <= coyoteTime);
        // 점프 쿨타임이 지났을 때 점프 가능
        bool canJumpFromCooldown = (Time.time - lastJumpTime >= jumpCooldownTime);

        // 점프 입력이 유효하고, 점프 가능 조건이 충족되면 점프 실행
        if (jumpBufferTimer > 0f && canJumpFromGroundOrCoyote && canJumpFromCooldown)
        {
            PerformJump();
        }
    }

    // 실제 점프를 수행하는 함수
    private void PerformJump()
    {
        if (isInteracting) return; // 상호작용 중에는 점프 불가

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); // 현재 수직 속도 초기화 (점프 높이 고정을 위해)
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); // 즉시 위로 힘 가함
        _isGrounded = false; // 점프했으므로 지면 아님
        animator.SetBool(hashIsGrounded, false); // 애니메이터도 업데이트
        animator.SetTrigger(hashJump); // 점프 애니메이션 트리거
        jumpBufferTimer = 0f; // 점프 버퍼 초기화
        lastJumpTime = Time.time; // 마지막 점프 시간 기록
    }

    private void ApplyMovement()
    {
        if (isInteracting)
        {
            // 상호작용 중에는 이동을 멈춥니다.
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime);
            rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
            return;
        }

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 moveInput = new Vector3(moveInputVec2.x, 0, moveInputVec2.y);

        if (moveInput.magnitude >= 0.1f)
        {
            Vector3 desiredMoveDirection = cameraTransform.TransformDirection(moveInput).normalized;
            desiredMoveDirection.y = 0;

            if (desiredMoveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), turnSpeed * Time.fixedDeltaTime);
            }
            Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed;
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, targetHorizontalVelocity, ref smoothDampVelocity, moveSmoothTime);
        }
        else
        {
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime);
        }
        rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
    }

    private void GroundCheck()
    {
        CapsuleCollider capCol = GetComponent<CapsuleCollider>();
        // 플레이어 발 아래에서 구체 캐스트를 사용하여 지면을 감지합니다.
        Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + groundCheckOffset);
        _isGrounded = Physics.CheckSphere(sphereOrigin, groundCheckDistance, groundMask);

        // 지면에 닿았을 때만 lastGroundedTime을 업데이트합니다.
        if (_isGrounded) lastGroundedTime = Time.time;
    }

    private void ApplyBetterGravity()
    {
        // 점프 정점 이후 더 빠르게 떨어지도록 중력을 조절합니다.
        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        // 점프 중 상승할 때 중력을 조절합니다. (버튼 누름 여부와 상관없이 고정)
        else if (rb.velocity.y > 0) // 이전: && inputActions.Player.Jump.phase != InputActionPhase.Performed 조건 제거
        {
            rb.velocity += Vector3.up * Physics.gravity.y * (gravityMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void UpdateAnimator()
    {
        // isInteracting 변수가 true일 때만 isEating을 true로 설정합니다.
        // 움직임이 감지되어 isInteracting이 false가 되면 isEating도 false가 됩니다.
        animator.SetBool(hashIsEating, isInteracting);

        // 상호작용 중이 아닐 때만 이동 및 지면 애니메이션을 업데이트합니다.
        if (!isInteracting)
        {
            float speedValue = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
            float normalizedSpeed = (speedValue > 0.1f) ? (isRunning ? 2f : 1f) : 0f;
            animator.SetFloat(hashSpeed, normalizedSpeed, 0.1f, Time.deltaTime);
            animator.SetBool(hashIsGrounded, _isGrounded);
        }
    }

    // --- 상호작용 로직 ---
    private void OnTriggerEnter(Collider other)
    {
        // InteractableBox 컴포넌트를 가진 오브젝트를 감지합니다.
        if (other.TryGetComponent<InteractableBox>(out var box))
        {
            currentInteractableBox = box;
            // 상호작용 가능한 상자를 발견하면 UI 슬라이더를 활성화합니다.
            if (box.interactionSlider != null)
            {
                box.interactionSlider.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 상호작용 범위를 벗어나면 상호작용을 취소하고 UI 슬라이더를 비활성화합니다.
        if (other.TryGetComponent<InteractableBox>(out var box) && box == currentInteractableBox)
        {
            CancelInteraction();
            if (box.interactionSlider != null)
            {
                box.interactionSlider.gameObject.SetActive(false);
            }
            currentInteractableBox = null;
        }
    }

    private void StartInteraction()
    {
        // 현재 상호작용 가능한 상자가 있고, 지면에 있으며, 다른 상호작용 코루틴이 실행 중이 아닐 때만 상호작용을 시작합니다.
        if (currentInteractableBox != null && _isGrounded && interactionCoroutine == null)
        {
            isInteracting = true; // 상호작용 상태를 true로 설정
            // isEating 불리언과 EatTrigger를 동시에 발사하여 애니메이션을 제어합니다.
            animator.SetBool(hashIsEating, true);
            animator.SetTrigger(hashEatTrigger);
            interactionCoroutine = StartCoroutine(HoldToInteract()); // 상호작용 코루틴 시작
        }
    }

    private void CancelInteraction()
    {
        // 상호작용 중이었다면 상태를 false로 변경합니다.
        if (isInteracting)
        {
            isInteracting = false;
        }

        // 상호작용 코루틴이 실행 중이었다면 중지하고 초기화합니다.
        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
            // 슬라이더 값을 0으로 초기화합니다.
            if (currentInteractableBox != null && currentInteractableBox.interactionSlider != null)
            {
                currentInteractableBox.interactionSlider.value = 0;
            }
            Debug.Log("상자 열기 취소");
        }
    }

    private IEnumerator HoldToInteract()
    {
        Debug.Log("상호작용 시작...");
        float timer = 0f;
        Slider slider = currentInteractableBox.interactionSlider;

        // 슬라이더의 value를 0으로 초기화
        if (slider != null)
        {
            slider.value = 0;
        }

        while (timer < interactionHoldDuration)
        {
            // isInteracting이 false가 되면 (이동 감지 등으로 인해) 코루틴을 즉시 종료합니다.
            if (!isInteracting)
            {
                yield break;
            }

            timer += Time.deltaTime;
            // 슬라이더의 value를 0에서 100까지 비율에 맞춰 업데이트합니다.
            if (slider != null)
            {
                slider.value = (timer / interactionHoldDuration) * 100f;
            }
            yield return null;
        }

        // 상호작용 성공 로직
        Debug.Log("상호작용 성공! 상자를 엽니다."); 
        // InteractableBox의 OpenBox() 함수를 호출하여 애니메이션과 오브젝트 제어를 위임합니다.
        currentInteractableBox.OpenBox();

        // 상호작용 완료 후 상태를 초기화하고 InteractableBox 컴포넌트를 제거합니다.
        isInteracting = false;
        Destroy(currentInteractableBox);
        currentInteractableBox = null;
    }

    void OnDrawGizmosSelected()
    {
        // Unity 에디터에서 지면 체크 범위를 시각화합니다.
        Gizmos.color = Color.green;
        CapsuleCollider capCol = GetComponent<CapsuleCollider>();
        Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + groundCheckOffset);
        Gizmos.DrawWireSphere(sphereOrigin, groundCheckDistance);
    }
}
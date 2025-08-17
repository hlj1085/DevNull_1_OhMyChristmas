using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Inventory))]
public class ReindeerController : MonoBehaviour
{
    [Header("랜덤 Idle 설정")]
    public float minIdleWaitTime = 3f; // Idle 애니메이션을 위한 최소 대기 시간
    public float maxIdleWaitTime = 7f; // Idle 애니메이션을 위한 최대 대기 시간

    [Header("이동 설정")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.6f;
    public float turnSpeed = 8f;
    public float moveSmoothTime = 0.1f;

    [Header("점프 설정")]
    public float jumpForce = 350f;
    public float jumpCooldownTime = 1f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    [Header("대쉬 설정")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 2f;
    public float dashSlideDuration = 0.5f;

    [Header("상호작용 설정")]
    public float interactionHoldDuration = 3f;
    public TextMeshProUGUI interactionPromptUI;
    public Slider interactionSlider;

    [Header("중력 및 지면 체크")]
    public float gravityMultiplier = 2.5f;
    public float fallMultiplier = 5f;
    public float groundCheckDistance = 0.2f;
    public float groundCheckOffset = 0.1f;
    public LayerMask groundMask;

    [Header("참조")]
    public Transform cameraTransform;
    public bool IsMoving => moveInputVec2.magnitude > 0.1f;

    private Rigidbody rb;
    private Animator animator;
    private Reindeer_Input inputActions;
    private Inventory inventory;
    private IInteractable currentInteractable;
    private Coroutine interactionCoroutine;

    private float idleTimer; // 멈춰있는 시간을 재는 타이머
    private float randomIdleWaitTime; // 다음 Idle 애니메이션까지 기다릴 랜덤 시간

    private Vector2 moveInputVec2;
    private bool isRunning;
    private bool _isGrounded;
    private bool isDashing;
    private float lastGroundedTime;
    private float jumpBufferTimer;
    private float lastJumpTime;
    private float lastDashTime = -Mathf.Infinity;
    private Vector3 currentHorizontalVelocity;
    private Vector3 smoothDampVelocity;

    private static readonly int hashSpeed = Animator.StringToHash("Speed");
    private static readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int hashJump = Animator.StringToHash("Jump");
    private static readonly int hashDash = Animator.StringToHash("Dash");
    private static readonly int hashIsEating = Animator.StringToHash("IsEating");
    private static readonly int hashIdleTrigger = Animator.StringToHash("IdleTrigger");



    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        inventory = GetComponent<Inventory>();
        rb.freezeRotation = true;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        inputActions = new Reindeer_Input();
        SetupInputCallbacks();

        if (interactionSlider != null) interactionSlider.gameObject.SetActive(false);
        if (interactionPromptUI != null) interactionPromptUI.gameObject.SetActive(false);
        ResetIdleTimer();
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    void Update()
    {
        HandleTimers();
        UpdateAnimator();
        UpdateInteractionUI();

        HandleRandomIdle();
    }

    void FixedUpdate()
    {
        if (isDashing) return;
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
        inputActions.Player.Jump.performed += ctx => jumpBufferTimer = jumpBufferTime;
        inputActions.Player.Dash.performed += _ => TryDash();
        inputActions.Player.Interact.started += _ => HandleInteractionStart();
        inputActions.Player.Interact.canceled += _ => HandleInteractionCancel();
    }

    private void UpdateInteractionUI()
    {
        if (interactionPromptUI != null)
        {
            // [수정] currentInteractable.CanInteract 조건을 추가
            if (currentInteractable != null && currentInteractable.CanInteract && interactionCoroutine == null)
            {
                string message = currentInteractable.GetInteractMessage();
                if (!string.IsNullOrEmpty(message))
                {
                    interactionPromptUI.text = message;
                    interactionPromptUI.gameObject.SetActive(true);
                }
                else
                {
                    interactionPromptUI.gameObject.SetActive(false);
                }
            }
            else
            {
                interactionPromptUI.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // [수정] 'InteractableBox'가 아닌, 'IInteractable' 자격증을 가진 모든 것을 찾습니다.
        if (other.TryGetComponent<IInteractable>(out var interactable))
        {
            currentInteractable = interactable;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // [수정] 범위를 벗어난 대상이 현재 기억하고 있는 대상과 같은지 확인합니다.
        if (other.TryGetComponent<IInteractable>(out var interactable) && interactable == currentInteractable)
        {
            HandleInteractionCancel(); // 꾹 누르던 중이었다면 취소
            currentInteractable = null; // 대상을 잊어버립니다.
        }
    }

    private void HandleInteractionStart()
    {
        // 상호작용 대상이 없거나, 상호작용이 불가능하거나, 대쉬 중이면 아무것도 하지 않습니다.
        if (currentInteractable == null || !currentInteractable.CanInteract || isDashing) return;

        // 대상의 타입에 따라 다른 행동을 합니다.
        if (currentInteractable.InteractionType == InteractionType.Instant)
        {
            currentInteractable.Interact(inventory);
        }
        else if (currentInteractable.InteractionType == InteractionType.Hold)
        {
            // 움직이는 중에는 홀드 상호작용을 시작할 수 없습니다.
            if (moveInputVec2.magnitude > 0.1f) return;

            if (interactionCoroutine == null)
            {
                interactionCoroutine = StartCoroutine(HoldInteractionCoroutine());
            }
        }
    }

    private void HandleInteractionCancel()
    {
        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
            if (interactionSlider != null)
            {
                interactionSlider.gameObject.SetActive(false);
                interactionSlider.value = 0;
            }
        }
    }

    // ReindeerController.cs

    private IEnumerator HoldInteractionCoroutine()
    {
        if (interactionSlider == null)
        {
            Debug.LogError("Hold 상호작용을 위한 Slider UI가 없습니다!");
            yield break;
        }

        interactionPromptUI?.gameObject.SetActive(false);
        interactionSlider.gameObject.SetActive(true);
        interactionSlider.value = 0;

        float timer = 0f;
        while (timer < interactionHoldDuration)
        {
            // [삭제] 코루틴 내부의 이동 감지 로직은 이제 필요 없습니다.
            // if (moveInputVec2.magnitude > 0.1f) { ... }

            timer += Time.deltaTime;
            interactionSlider.value = timer / interactionHoldDuration;
            yield return null;
        }

        interactionSlider.gameObject.SetActive(false);
        currentInteractable?.Interact(inventory);
        interactionCoroutine = null;

    }

    private void HandleTimers() { if (jumpBufferTimer > 0f && (_isGrounded || Time.time - lastGroundedTime <= coyoteTime) && (Time.time - lastJumpTime >= jumpCooldownTime)) { PerformJump(); } jumpBufferTimer -= Time.deltaTime; }
    private void PerformJump() { if (interactionCoroutine != null || isDashing) return; rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); _isGrounded = false; animator.SetBool(hashIsGrounded, false); animator.SetTrigger(hashJump); jumpBufferTimer = 0f; lastJumpTime = Time.time; }
    private void TryDash() { if (!isDashing && Time.time >= lastDashTime + dashCooldown && _isGrounded) { StartCoroutine(DashCoroutine()); } }
    private void ApplyMovement()
    {
        if (interactionCoroutine != null || isDashing) { return; }

        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        // [수정된 이동 방향 계산 로직]
        if (moveInputVec2.magnitude >= 0.1f)
        {
            // 1. 카메라의 '앞쪽'과 '오른쪽' 방향을 기준으로 삼습니다.
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            // 2. y축 값을 0으로 만들어, 땅에 평행한 방향 벡터로 만듭니다.
            cameraForward.y = 0;
            cameraRight.y = 0;

            // 3. 길이를 1로 정규화하여 방향 순수성 유지
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 4. 이 두 방향과 키보드 입력을 조합하여 최종 이동 방향을 계산합니다.
            // (moveInputVec2.y는 W/S, moveInputVec2.x는 A/D 입력값입니다)
            Vector3 desiredMoveDirection = cameraForward * moveInputVec2.y + cameraRight * moveInputVec2.x;

            // 캐릭터 회전 로직
            if (desiredMoveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), turnSpeed * Time.fixedDeltaTime);
            }

            // 속도 적용 로직
            Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed;
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, targetHorizontalVelocity, ref smoothDampVelocity, moveSmoothTime);
        }
        else
        {
            // 멈출 때의 로직
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime);
        }

        rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
    }
    private IEnumerator DashCoroutine() { isDashing = true; lastDashTime = Time.time; animator.SetTrigger(hashDash); Vector3 moveDirection = new Vector3(moveInputVec2.x, 0, moveInputVec2.y); Vector3 dashDirection = transform.forward; if (moveDirection.magnitude > 0.1f) { dashDirection = cameraTransform.TransformDirection(moveDirection).normalized; dashDirection.y = 0; } float startTime = Time.time; while (Time.time < startTime + dashDuration) { rb.velocity = new Vector3(dashDirection.x * dashSpeed, 0, dashDirection.z * dashSpeed); yield return new WaitForFixedUpdate(); } float slideStartTime = Time.time; Vector3 slideStartVelocity = rb.velocity; Vector3 finalVelocity = new Vector3(0, rb.velocity.y, 0); while (Time.time < slideStartTime + dashSlideDuration) { float t = (Time.time - slideStartTime) / dashSlideDuration; rb.velocity = Vector3.Lerp(slideStartVelocity, finalVelocity, t); yield return new WaitForFixedUpdate(); } isDashing = false; }
    private void GroundCheck() { CapsuleCollider capCol = GetComponent<CapsuleCollider>(); Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + groundCheckOffset); _isGrounded = Physics.CheckSphere(sphereOrigin, groundCheckDistance, groundMask); if (_isGrounded) lastGroundedTime = Time.time; }
    private void ApplyBetterGravity() { if (rb.velocity.y < 0) { rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime; } else if (rb.velocity.y > 0) { rb.velocity += Vector3.up * Physics.gravity.y * (gravityMultiplier - 1) * Time.fixedDeltaTime; } }
    private void UpdateAnimator()
    {
        // [수정된 로직]
        // 만약 '꾹 누르기' 상호작용 코루틴이 실행 중이라면,
        if (interactionCoroutine != null)
        {
            // 먹는 애니메이션을 켜고, 이동 애니메이션은 멈춥니다.
            animator.SetBool(hashIsEating, true);
            animator.SetFloat(hashSpeed, 0f, 0.1f, Time.deltaTime);
            return; // 아래의 다른 애니메이션 로직은 실행하지 않고 함수를 종료합니다.
        }

        // '꾹 누르기' 중이 아닐 때는, 먹는 애니메이션을 끕니다.
        animator.SetBool(hashIsEating, false);

        // --- 아래는 기존의 이동/달리기 애니메이션 로직 ---
        float speedValue = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
        float normalizedSpeed = (speedValue > 0.1f) ? (isRunning ? 2f : 1f) : 0f;
        animator.SetFloat(hashSpeed, normalizedSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(hashIsGrounded, _isGrounded);
    }
    private void HandleRandomIdle()
    {
        // [수정된 로직] 애니메이터의 현재 상태 정보를 가져옵니다.
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

        // [수정된 조건] 애니메이터가 'Stop' 상태일 때만 타이머를 작동시킵니다.
        if (currentState.IsName("Stop"))
        {
            // 멈춤 상태라면 타이머 시간을 증가시킴
            idleTimer += Time.deltaTime;

            // 타이머가 지정된 랜덤 대기 시간을 초과했다면
            if (idleTimer >= randomIdleWaitTime)
            {
                // IdleTrigger를 발동시키고 타이머를 리셋
                animator.SetTrigger(hashIdleTrigger);
                ResetIdleTimer();
            }
        }
        else
        {
            // 'Stop' 상태가 아니라면 (움직이거나, 점프하거나, Idle 애니메이션 중이라면)
            // 타이머를 계속 리셋합니다.
            ResetIdleTimer();
        }
    }

    // [추가] Idle 타이머를 리셋하고 새로운 랜덤 시간을 뽑는 함수
    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        randomIdleWaitTime = Random.Range(minIdleWaitTime, maxIdleWaitTime);
    }
}
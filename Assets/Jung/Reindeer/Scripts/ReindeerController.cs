using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;

// 포톤 상태 동기화를 위해 IPunObservable 인터페이스를 추가합니다.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Inventory))]
public class ReindeerController : MonoBehaviour, IPunObservable
{
    // 순록의 현재 상태를 정의합니다.
    public enum PlayerState
    {
        Normal,    // 정상
        Stunned,   // 기절 (F키 연타로 회복 가능)
        Captured   // 포획됨 (일정 시간 후 자동 탈출)
    }

    [Header("포획 관련 참조")]
    [Tooltip("순록의 모델, 파티클 등 시각적 요소를 모두 담고 있는 부모 오브젝트")]
    public Transform sackTransform; // 카메라가 대신 바라볼 보따리
    public GameObject reindeerVisuals;

    [Header("기절 및 포획 설정")]
    public float recoveryTime = 20f; // 회복에 필요한 총 진행도 (20초)
    [Tooltip("기절 상태에서 F키 연타 시 추가되는 진행도")]
    public float stunMashAmount = 0.5f; // F키 누를 때마다 0.5초 분량 추가
    [Tooltip("포획 상태에서 F키 연타 시 추가되는 진행도")]
    public float capturedMashAmount = 0.1f; // F키 누를 때마다 0.1초 분량 추가
    [Tooltip("F키 연타 입력 사이의 최소 간격(쿨타임)")]
    public float mashCooldown = 0.5f;

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

    [Header("랜덤 Idle 설정")]
    public float minIdleWaitTime = 3f;
    public float maxIdleWaitTime = 7f;

    [Header("중력 및 지면 체크")]
    public float gravityMultiplier = 2.5f;
    public float fallMultiplier = 5f;
    public float groundCheckDistance = 0.2f;
    public float groundCheckOffset = 0.1f;
    public LayerMask groundMask;

    [Header("참조")]
    [Tooltip("순록 프리팹에 포함된 플레이어 전용 카메라 게임 오브젝트")]
    public GameObject playerCameraObject;
    [Tooltip("순록 카메라에 붙어있는 ThirdPersonCamera 스크립트")]
    public ThirdPersonCamera thirdPersonCameraScript; // <<< 이 변수를 사용합니다.
    public Transform cameraTransform;
    public bool IsMoving => moveInputVec2.magnitude > 0.1f;

    [Header("UI 그룹 참조")]
    public GameObject interactionUIGroup;
    public GameObject recoveryUIGroup;
    public TextMeshProUGUI interactionPromptUI;
    public Slider interactionSlider;
    public Slider recoverySlider;

    // --- 비공개 변수 ---
    private Transform currentSackTransform; // 현재 내가 잡혀있는 보따리의 Transform

    private PlayerState currentState = PlayerState.Normal;
    public PlayerState CurrentState => currentState;

    private float lastMashTime; // 마지막으로 F키를 누른 시간을 기록
    private float currentRecoveryTimer;

    private PhotonView photonView;
    private Rigidbody rb;
    private Animator animator;
    private Reindeer_Input inputActions;
    private Inventory inventory;
    private IInteractable currentInteractable;
    private Coroutine interactionCoroutine;
    private float idleTimer;
    private float randomIdleWaitTime;
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




    // --- 애니메이터 해시 ---
    private static readonly int hashSpeed = Animator.StringToHash("Speed");
    private static readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int hashJump = Animator.StringToHash("Jump");
    private static readonly int hashDash = Animator.StringToHash("Dash");
    private static readonly int hashIsEating = Animator.StringToHash("IsEating");
    private static readonly int hashIdleTrigger = Animator.StringToHash("IdleTrigger");
    private static readonly int hashStun = Animator.StringToHash("Stun");
    private static readonly int hashState = Animator.StringToHash("State");

    void Awake()
    {
        photonView = GetComponent<PhotonView>();

        if (!photonView.IsMine)
        {
            this.enabled = false;
            if (cameraTransform != null) cameraTransform.gameObject.SetActive(false);
            var audioListener = GetComponentInChildren<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
            return;
        }

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

        if (interactionUIGroup != null) interactionUIGroup.SetActive(false);
        if (interactionSlider != null) interactionSlider.gameObject.SetActive(false);
        if (recoveryUIGroup != null) recoveryUIGroup.SetActive(false);

        ResetIdleTimer();
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    void Update()
    {
        if (!photonView.IsMine) return;

        HandleDebugInput();

        HandleTimers();
        UpdateAnimator();
        UpdateInteractionUI();
        HandleRandomIdle();
        HandleRecoveryMash();
        UpdateRecoveryAndState();
    }

    /// <summary>
    /// 디버깅 및 테스트를 위한 임시 입력 처리 함수입니다.
    /// </summary>
    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("디버그: '1' 키 입력 - 기절(Stun) RPC를 호출합니다.");
            photonView.RPC("GetStunned", RpcTarget.All);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (currentState == PlayerState.Stunned)
            {
                Debug.Log("디버그: '2' 키 입력 - 포획(Capture) RPC를 호출합니다.");
                photonView.RPC("GetCaptured", RpcTarget.All);
            }
            else
            {
                Debug.LogWarning("디버그: 포획은 '기절' 상태에서만 가능합니다.");
            }
        }
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        if (isDashing) return;
        GroundCheck();
        ApplyMovement();
        ApplyBetterGravity();
    }

    // --- 상태 관리 및 동기화 ---

    [PunRPC]
    public void GetStunned()
    {
        if (currentState != PlayerState.Normal) return;

        currentState = PlayerState.Stunned;
        currentRecoveryTimer = 0f; // 진행도를 0에서 시작
        lastMashTime = -mashCooldown; // 즉시 첫 입력이 가능하도록 쿨타임 초기화
        animator.SetTrigger(hashStun);
    }

    [PunRPC]
    public void GetCaptured(int sackPhotonViewID) // <<< 이 부분이 (int sackPhotonViewID) 인지 반드시 확인!
    {
        if (currentState != PlayerState.Stunned) return;

        // 전달받은 ID로 씬에서 해당 보따리를 찾습니다.
        PhotonView sackPhotonView = PhotonView.Find(sackPhotonViewID);
        if (sackPhotonView == null)
        {
            Debug.LogError("포획 RPC 오류: 전달받은 ID의 보따리를 찾을 수 없습니다!");
            return;
        }

        // 찾은 보따리의 Transform을 저장합니다.
        currentSackTransform = sackPhotonView.transform;

        currentState = PlayerState.Captured;
        currentRecoveryTimer = 0f;
        lastMashTime = -mashCooldown;

        // --- 포획 연출 로직 ---
        if (reindeerVisuals != null)
        {
            reindeerVisuals.SetActive(false); // 순록 모델 숨기기
        }

        // 카메라 타겟을 내가 잡힌 바로 그 보따리로 변경
        if (photonView.IsMine && thirdPersonCameraScript != null)
        {
            thirdPersonCameraScript.target = sackTransform;
        }
    }

    private void HandleRecoveryMash()
    {
        // 기절 또는 포획 상태가 아니면 실행하지 않음
        if (currentState != PlayerState.Stunned && currentState != PlayerState.Captured) return;

        // F키(상호작용 키)가 눌렸는지 확인
        if (inputActions.Player.Recovery.triggered)
        {
            // 마지막 입력으로부터 쿨타임(0.5초)이 지났는지 확인
            if (Time.time >= lastMashTime + mashCooldown)
            {
                // 마지막 입력 시간 갱신
                lastMashTime = Time.time;

                // 현재 상태에 따라 추가할 진행도를 결정
                float amountToAdd = (currentState == PlayerState.Stunned) ? stunMashAmount : capturedMashAmount;

                // 모든 클라이언트에게 진행도를 더하라고 RPC 전송
                photonView.RPC("AddRecoveryProgress", RpcTarget.All, amountToAdd);
            }
        }
    }

    [PunRPC]
    public void AddRecoveryProgress(float amount)
    {
        // 기절 또는 포획 상태일 때만 진행도를 더함
        if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
        {
            currentRecoveryTimer += amount;
            // 진행도가 최대값(recoveryTime)을 넘지 않도록 제한
            currentRecoveryTimer = Mathf.Clamp(currentRecoveryTimer, 0f, recoveryTime);
        }
    }

    private void UpdateRecoveryAndState()
    {
        if (recoveryUIGroup == null || recoverySlider == null) return;

        bool isRecovering = (currentState == PlayerState.Stunned || currentState == PlayerState.Captured);
        recoveryUIGroup.SetActive(isRecovering);

        if (isRecovering)
        {
            // 자동으로 진행도 증가 (시간이 흐름)
            currentRecoveryTimer += Time.deltaTime;

            // UI 슬라이더 업데이트 (0% -> 100%)
            // 포획 상태일 때의 총 필요량은 recoveryTime을 그대로 사용. 
            // 만약 다른 값을 쓰고 싶다면 capturedEscapeTime 변수 등을 활용하여 수정 가능
            recoverySlider.value = currentRecoveryTimer / recoveryTime;

            // 진행도가 100%에 도달하면 상태 변경
            if (currentRecoveryTimer >= recoveryTime)
            {
                // [변경] 포획 상태였다면, 탈출 RPC를 호출
                if (currentState == PlayerState.Captured)
                {
                    photonView.RPC("ReleaseFromCapture", RpcTarget.All);
                }

                currentState = PlayerState.Normal;
            }
        }
    }
    [PunRPC]
    public void ReleaseFromCapture()
    {
        if (currentSackTransform == null) return; // 현재 잡혀있는 보따리가 없으면 실행 안함

        // --- 탈출 연출 로직 ---
        // 1. 물리적 충돌을 막기 위해 속도를 0으로 초기화합니다.
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. Rigidbody를 통해 안전하게 위치를 보따리 위치로 이동시킵니다.
        if (rb != null)
        {
            rb.MovePosition(currentSackTransform.position);
        }
        else // 혹시 Rigidbody가 없는 경우를 대비한 예비 코드
        {
            this.transform.position = currentSackTransform.position;
        }


        // 3. 순록 모델 다시 보이기
        if (reindeerVisuals != null)
        {
            reindeerVisuals.SetActive(true);
        }

        // 4. 카메라 타겟을 다시 순록으로 변경
        if (photonView.IsMine && thirdPersonCameraScript != null)
        {
            thirdPersonCameraScript.target = this.transform;
        }

        // 5. 보따리 참조를 비워서 정리
        currentSackTransform = null;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentState);
            stream.SendNext(currentRecoveryTimer);
        }
        else
        {
            this.currentState = (PlayerState)stream.ReceiveNext();
            this.currentRecoveryTimer = (float)stream.ReceiveNext();
        }
    }

    // --- 입력 및 상호작용 로직 ---

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

    // --- [핵심 수정] 상호작용 UI 로직 전체 수정 ---
    private void UpdateInteractionUI()
    {
        if (interactionUIGroup == null || interactionPromptUI == null || interactionSlider == null) return;

        // 1. 상호작용이 가능한 상태인지 먼저 확인
        bool canInteract = (currentInteractable != null && currentInteractable.CanInteract && currentState == PlayerState.Normal);

        // 2. 상호작용 가능 여부에 따라 UI 그룹(텍스트 배경 등) 활성화/비활성화
        interactionUIGroup.SetActive(canInteract);

        // 3. 상호작용이 불가능하면, 슬라이더도 확실히 끄고 함수 종료
        if (!canInteract)
        {
            if (interactionSlider.gameObject.activeSelf)
            {
                interactionSlider.gameObject.SetActive(false);
            }
            return;
        }

        // 4. 상호작용이 가능하면, 프롬프트 텍스트 설정
        interactionPromptUI.text = currentInteractable.GetInteractMessage();

        // 5. 상호작용 타입을 확인하여 슬라이더 표시 여부 결정
        bool isHoldType = currentInteractable.InteractionType == InteractionType.Hold;

        // 홀드 타입이면 슬라이더를 활성화하고, 아니면 비활성화
        if (interactionSlider.gameObject.activeSelf != isHoldType)
        {
            interactionSlider.gameObject.SetActive(isHoldType);
        }

        // 홀드 타입이고, 아직 홀드를 시작 안했다면 슬라이더 값을 0으로 초기화
        if (isHoldType && interactionCoroutine == null)
        {
            interactionSlider.value = 0;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<IInteractable>(out var interactable))
        {
            currentInteractable = interactable;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<IInteractable>(out var interactable) && interactable == currentInteractable)
        {
            HandleInteractionCancel();
            currentInteractable = null;
        }
    }

    private void HandleInteractionStart()
    {
        if (currentState != PlayerState.Normal) return;
        if (currentInteractable == null || !currentInteractable.CanInteract || isDashing) return;

        if (currentInteractable.InteractionType == InteractionType.Instant)
        {
            currentInteractable.Interact(inventory);
        }
        else if (currentInteractable.InteractionType == InteractionType.Hold)
        {
            if (IsMoving) return;
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
            // 홀드 취소 시, 슬라이더 값을 초기화 (슬라이더 자체는 UpdateInteractionUI가 관리)
            if (interactionSlider != null)
            {
                interactionSlider.value = 0;
            }
        }
    }

    private IEnumerator HoldInteractionCoroutine()
    {
        if (interactionSlider == null) yield break;
        // 이제 이 코루틴은 슬라이더를 켜는 대신, 값만 채워줌

        float timer = 0f;
        while (timer < interactionHoldDuration)
        {
            timer += Time.deltaTime;
            interactionSlider.value = timer / interactionHoldDuration;
            yield return null;
        }

        currentInteractable?.Interact(inventory);

        interactionCoroutine = null;

        // 홀드 성공 후, 슬라이더 값을 0으로 초기화
        interactionSlider.value = 0;
    }

    // --- 기본 행동 로직 (이전과 동일) ---

    private void HandleTimers()
    {
        bool hasJumpBuffer = jumpBufferTimer > 0f;
        bool canUseCoyoteTime = (Time.time - lastGroundedTime <= coyoteTime);
        bool isReadyToJump = _isGrounded || canUseCoyoteTime;
        bool isJumpCooledDown = (Time.time - lastJumpTime >= jumpCooldownTime);

        if (hasJumpBuffer && isReadyToJump && isJumpCooledDown)
        {
            PerformJump();
        }

        jumpBufferTimer -= Time.deltaTime;
    }

    private void PerformJump()
    {
        if (currentState != PlayerState.Normal || interactionCoroutine != null || isDashing) return;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _isGrounded = false;
        animator.SetBool(hashIsGrounded, false);
        animator.SetTrigger(hashJump);
        jumpBufferTimer = 0f;
        lastJumpTime = Time.time;
    }

    private void TryDash()
    {
        bool canDash = (currentState == PlayerState.Normal);
        bool isDashCooledDown = (Time.time >= lastDashTime + dashCooldown);

        if (canDash && !isDashing && isDashCooledDown && _isGrounded)
        {
            StartCoroutine(DashCoroutine());
        }
    }

    private void ApplyMovement()
    {
        if (currentState != PlayerState.Normal || interactionCoroutine != null || isDashing)
        {
            Vector3 targetStopVelocity = Vector3.zero;
            currentHorizontalVelocity = Vector3.SmoothDamp(
                currentHorizontalVelocity,
                targetStopVelocity,
                ref smoothDampVelocity,
                moveSmoothTime
            );
            rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
            return;
        }

        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        if (moveInputVec2.magnitude >= 0.1f)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();
            Vector3 desiredMoveDirection = (cameraForward * moveInputVec2.y + cameraRight * moveInputVec2.x).normalized;

            if (desiredMoveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            }

            Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed;
            currentHorizontalVelocity = Vector3.SmoothDamp(
                currentHorizontalVelocity,
                targetHorizontalVelocity,
                ref smoothDampVelocity,
                moveSmoothTime
            );
        }
        else
        {
            Vector3 targetStopVelocity = Vector3.zero;
            currentHorizontalVelocity = Vector3.SmoothDamp(
                currentHorizontalVelocity,
                targetStopVelocity,
                ref smoothDampVelocity,
                moveSmoothTime
            );
        }

        rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        animator.SetTrigger(hashDash);

        Vector3 moveDirection = new Vector3(moveInputVec2.x, 0, moveInputVec2.y);
        Vector3 dashDirection = transform.forward;
        if (moveDirection.magnitude > 0.1f)
        {
            dashDirection = cameraTransform.TransformDirection(moveDirection).normalized;
            dashDirection.y = 0;
        }

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            Vector3 dashVelocity = new Vector3(dashDirection.x * dashSpeed, 0, dashDirection.z * dashSpeed);
            rb.velocity = dashVelocity;
            yield return new WaitForFixedUpdate();
        }

        float slideStartTime = Time.time;
        Vector3 slideStartVelocity = rb.velocity;
        Vector3 finalVelocity = new Vector3(0, rb.velocity.y, 0);
        while (Time.time < slideStartTime + dashSlideDuration)
        {
            float t = (Time.time - slideStartTime) / dashSlideDuration;
            rb.velocity = Vector3.Lerp(slideStartVelocity, finalVelocity, t);
            yield return new WaitForFixedUpdate();
        }

        isDashing = false;
    }

    private void GroundCheck()
    {
        CapsuleCollider capCol = GetComponent<CapsuleCollider>();
        float sphereRadius = groundCheckDistance;
        Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + sphereRadius - groundCheckOffset);
        _isGrounded = Physics.CheckSphere(sphereOrigin, sphereRadius, groundMask);

        if (_isGrounded)
        {
            lastGroundedTime = Time.time;
        }
    }

    private void ApplyBetterGravity()
    {
        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.velocity.y > 0)
        {
            rb.velocity += Vector3.up * Physics.gravity.y * (gravityMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void UpdateAnimator()
    {
        animator.SetInteger(hashState, (int)currentState);
        animator.SetBool(hashIsEating, interactionCoroutine != null);
        if (currentState != PlayerState.Normal || interactionCoroutine != null)
        {
            animator.SetFloat(hashSpeed, 0f, 0.1f, Time.deltaTime);
            return;
        }
        float speedValue = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
        float normalizedSpeed = (speedValue > 0.1f) ? (isRunning ? 2f : 1f) : 0f;
        animator.SetFloat(hashSpeed, normalizedSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(hashIsGrounded, _isGrounded);
    }

    private void HandleRandomIdle()
    {
        AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (currentStateInfo.IsName("Stop"))
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= randomIdleWaitTime)
            {
                animator.SetTrigger(hashIdleTrigger);
                ResetIdleTimer();
            }
        }
        else
        {
            ResetIdleTimer();
        }
    }

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        randomIdleWaitTime = Random.Range(minIdleWaitTime, maxIdleWaitTime);
    }
}
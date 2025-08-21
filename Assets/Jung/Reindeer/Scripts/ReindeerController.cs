using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;

// 포톤 동기화 및 상호작용 인터페이스를 구현합니다.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Inventory))]
public class ReindeerController : MonoBehaviour, IPunObservable, IInteractable
{
    // 플레이어의 현재 상태를 정의합니다.
    public enum PlayerState
    {
        Normal,
        Stunned,
        Captured
    }

    [Header("참조")]
    [Tooltip("순록 프리팹에 포함된 플레이어 전용 카메라 게임 오브젝트")]
    public GameObject playerCameraObject;
    [Tooltip("순록 카메라에 붙어있는 ThirdPersonCamera 스크립트")]
    public ThirdPersonCamera thirdPersonCameraScript;
    [Tooltip("포획 시 사용할 보따리 오브젝트의 Transform (테스트용)")]
    public Transform sackTransform;
    [Tooltip("순록의 모델, 파티클 등 시각적 요소를 모두 담고 있는 부모 오브젝트")]
    public GameObject reindeerVisuals;

    [Header("기절 및 포획 설정")]
    public float recoveryTime = 20f; // 회복에 필요한 총 진행도
    [Tooltip("기절 상태에서 F키 연타 시 추가되는 진행도")]
    public float stunMashAmount = 0.5f;
    [Tooltip("포획 상태에서 F키 연타 시 추가되는 진행도")]
    public float capturedMashAmount = 0.1f;
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

    // 외부에서 현재 상태를 읽기 위한 프로퍼티
    public PlayerState CurrentState => currentState;
    public bool IsMoving => moveInputVec2.magnitude > 0.1f;

    // --- UI 참조 변수 (UIManager를 통해 할당됨) ---
    private GameObject interactionUIGroup;
    private GameObject recoveryUIGroup;
    private TextMeshProUGUI interactionPromptUI;
    private Slider interactionSlider;
    private Slider recoverySlider;

    // --- 내부 로직 변수 ---
    private Transform currentSackTransform;
    private PlayerState currentState = PlayerState.Normal;
    private float lastMashTime;
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
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        inventory = GetComponent<Inventory>();

        // 내 캐릭터가 아닐 경우, 카메라와 입력 시스템만 비활성화하고 스크립트 자체는 켜둡니다. (RPC 수신을 위해)
        if (!photonView.IsMine)
        {
            if (playerCameraObject != null) playerCameraObject.SetActive(false);
            if (thirdPersonCameraScript != null) thirdPersonCameraScript.enabled = false;
        }
        else // 내 캐릭터일 경우에만 입력 시스템을 초기화합니다.
        {
            inputActions = new Reindeer_Input();
            SetupInputCallbacks();
        }

        rb.freezeRotation = true;
        ResetIdleTimer();
    }

    void Start()
    {
        // 내 캐릭터일 경우에만 UI 매니저를 통해 UI를 연결하고 초기화합니다.
        if (photonView.IsMine)
        {
            if (UIManager.instance != null)
            {
                // UIManager에게 자신의 인벤토리를 등록합니다.
                UIManager.instance.SetInventory(inventory);

                // UIManager로부터 UI 참조를 받아옵니다.
                interactionUIGroup = UIManager.instance.interactionUIGroup;
                recoveryUIGroup = UIManager.instance.recoveryUIGroup;
                interactionPromptUI = UIManager.instance.interactionPromptUI;
                interactionSlider = UIManager.instance.interactionSlider;
                recoverySlider = UIManager.instance.recoverySlider;

                // 게임 시작 시 UI들을 확실하게 꺼줍니다.
                if (interactionUIGroup != null) interactionUIGroup.SetActive(false);
                if (recoveryUIGroup != null) recoveryUIGroup.SetActive(false);
            }
            else
            {
                Debug.LogError("씬에 UIManager가 없습니다!");
            }
        }
    }

    private void OnEnable()
    {
        if (photonView.IsMine)
        {
            inputActions?.Enable();
        }
    }

    private void OnDisable()
    {
        if (photonView.IsMine)
        {
            inputActions?.Disable();
        }
    }

    void Update()
    {
        // --- 모든 클라이언트에서 공통으로 실행되어야 하는 로직 ---
        // 다른 사람의 기절/포획 상태와 타이머도 보여야 하므로 IsMine 체크 밖에 둡니다.
        UpdateRecoveryAndState();

        // --- 내 캐릭터(로컬 플레이어)일 때만 실행되는 로직 ---
        if (!photonView.IsMine)
        {
            // 내 캐릭터가 아니면 여기서 즉시 종료합니다.
            return;
        }

        // 이제 이 아래의 모든 코드는 오직 내가 조종하는 캐릭터에서만 실행됩니다.
        UpdateAnimator();       // ★★★★★ 애니메이션 제어를 여기로 이동!
        HandleDebugInput();
        HandleTimers();
        UpdateAllUI();
        HandleRandomIdle();
        HandleRecoveryMash();
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        if (isDashing) return;
        GroundCheck();
        ApplyMovement();
        ApplyBetterGravity();
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            photonView.RPC("GetStunned", RpcTarget.All);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) && currentState == PlayerState.Stunned)
        {
            // 실제로는 산타가 보따리의 PhotonView ID를 담아서 호출해야 함
            // 테스트를 위해 임시로 sackTransform의 PhotonView ID를 사용 (없으면 0)
            int sackId = (sackTransform != null && sackTransform.GetComponent<PhotonView>() != null) ? sackTransform.GetComponent<PhotonView>().ViewID : 0;
            photonView.RPC("GetCaptured", RpcTarget.All, sackId);
        }
    }

    // --- 상태 관리 및 동기화 (RPC) ---

    [PunRPC]
    public void GetStunned()
    {
        if (currentState != PlayerState.Normal) return;
        currentState = PlayerState.Stunned;
        currentRecoveryTimer = 0f;
        lastMashTime = -mashCooldown;
        animator.SetTrigger(hashStun);
    }

    [PunRPC]
    public void GetCaptured(int sackPhotonViewID)
    {
        if (currentState != PlayerState.Stunned) return;

        PhotonView sackPhotonView = PhotonView.Find(sackPhotonViewID);
        if (sackPhotonView == null)
        {
            Debug.LogError("포획 RPC 오류: ID " + sackPhotonViewID + "의 보따리를 찾을 수 없습니다!");
            return;
        }

        currentSackTransform = sackPhotonView.transform;
        currentState = PlayerState.Captured;
        currentRecoveryTimer = 0f;
        lastMashTime = -mashCooldown;

        if (reindeerVisuals != null)
        {
            reindeerVisuals.SetActive(false);
        }

        if (photonView.IsMine && thirdPersonCameraScript != null)
        {
            thirdPersonCameraScript.target = currentSackTransform;
        }
    }

    [PunRPC]
    public void GetRescued()
    {
        if (currentState != PlayerState.Normal)
        {
            if (currentState == PlayerState.Captured)
            {
                ReleaseFromCapture();
            }
            currentState = PlayerState.Normal;
        }
    }

    [PunRPC]
    public void ReleaseFromCapture()
    {
        if (currentSackTransform == null) return;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.MovePosition(currentSackTransform.position);
        }
        else
        {
            transform.position = currentSackTransform.position;
        }

        if (reindeerVisuals != null)
        {
            reindeerVisuals.SetActive(true);
        }

        if (photonView.IsMine && thirdPersonCameraScript != null)
        {
            thirdPersonCameraScript.target = this.transform;
        }

        currentSackTransform = null;
    }

    [PunRPC]
    public void AddRecoveryProgress(float amount)
    {
        if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
        {
            currentRecoveryTimer += amount;
            currentRecoveryTimer = Mathf.Clamp(currentRecoveryTimer, 0f, recoveryTime);
        }
    }

    // --- 상태 및 UI 업데이트 ---

    private void UpdateRecoveryAndState()
    {
        if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
        {
            currentRecoveryTimer += Time.deltaTime;

            if (currentRecoveryTimer >= recoveryTime)
            {
                if (currentState == PlayerState.Captured)
                {
                    photonView.RPC("ReleaseFromCapture", RpcTarget.All);
                }
                currentState = PlayerState.Normal;
            }
        }
    }

    private void UpdateAllUI()
    {
        if (recoveryUIGroup == null || interactionUIGroup == null) return;

        if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
        {
            recoveryUIGroup.SetActive(true);
            interactionUIGroup.SetActive(false);
            if (recoverySlider != null)
            {
                recoverySlider.value = currentRecoveryTimer / recoveryTime;
            }
        }
        else if (currentState == PlayerState.Normal)
        {
            recoveryUIGroup.SetActive(false);
            bool canInteract = (currentInteractable != null && currentInteractable.CanInteract);
            interactionUIGroup.SetActive(canInteract);

            if (canInteract)
            {
                if (interactionPromptUI != null)
                    interactionPromptUI.text = currentInteractable.GetInteractMessage();

                if (interactionSlider != null)
                {
                    bool isHoldType = currentInteractable.InteractionType == InteractionType.Hold;
                    interactionSlider.gameObject.SetActive(isHoldType);
                    if (isHoldType && interactionCoroutine == null)
                    {
                        interactionSlider.value = 0;
                    }
                }
            }
        }
        else
        {
            recoveryUIGroup.SetActive(false);
            interactionUIGroup.SetActive(false);
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

    // --- IInteractable (다른 플레이어가 구출) ---
    public InteractionType InteractionType => InteractionType.Hold;
    public bool CanInteract => currentState == PlayerState.Stunned || currentState == PlayerState.Captured;
    public string GetInteractMessage()
    {
        if (currentState == PlayerState.Stunned) return "Help player";
        if (currentState == PlayerState.Captured) return "Help2";
        return "";
    }
    public void Interact(Inventory interactorInventory)
    {
        if (photonView != null)
        {
            photonView.RPC("GetRescued", RpcTarget.All);
        }
    }

    // --- 입력 콜백 및 상호작용 로직 ---
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
        inputActions.Player.Recovery.performed += _ => HandleRecoveryMash();
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
        if (currentInteractable == null || !currentInteractable.CanInteract || CurrentState != PlayerState.Normal) return;
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
    private void HandleInteractionCancel() { if (interactionCoroutine != null) { StopCoroutine(interactionCoroutine); interactionCoroutine = null; if (interactionSlider != null) { interactionSlider.value = 0; } } }
    private IEnumerator HoldInteractionCoroutine() { if (interactionSlider == null) yield break; float timer = 0f; while (timer < interactionHoldDuration) { timer += Time.deltaTime; interactionSlider.value = timer / interactionHoldDuration; yield return null; } currentInteractable?.Interact(inventory); interactionCoroutine = null; if (interactionSlider != null) interactionSlider.value = 0; }
    private void HandleRecoveryMash() { if (inputActions == null || (currentState != PlayerState.Stunned && currentState != PlayerState.Captured)) return; if (inputActions.Player.Recovery.triggered) { if (Time.time >= lastMashTime + mashCooldown) { lastMashTime = Time.time; float amountToAdd = (currentState == PlayerState.Stunned) ? stunMashAmount : capturedMashAmount; photonView.RPC("AddRecoveryProgress", RpcTarget.All, amountToAdd); } } }

    // --- 기본 행동 로직 ---
    private void HandleTimers() { bool hasJumpBuffer = jumpBufferTimer > 0f; bool canUseCoyoteTime = (Time.time - lastGroundedTime <= coyoteTime); bool isReadyToJump = _isGrounded || canUseCoyoteTime; bool isJumpCooledDown = (Time.time - lastJumpTime >= jumpCooldownTime); if (hasJumpBuffer && isReadyToJump && isJumpCooledDown) { PerformJump(); } jumpBufferTimer -= Time.deltaTime; }
    private void PerformJump() { if (currentState != PlayerState.Normal || interactionCoroutine != null || isDashing) return; rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); _isGrounded = false; animator.SetBool(hashIsGrounded, false); animator.SetTrigger(hashJump); jumpBufferTimer = 0f; lastJumpTime = Time.time; }
    private void TryDash() { if (currentState != PlayerState.Normal || isDashing || !(Time.time >= lastDashTime + dashCooldown) || !_isGrounded) return; StartCoroutine(DashCoroutine()); }
    private void ApplyMovement() { if (currentState != PlayerState.Normal || interactionCoroutine != null || isDashing) { currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime); rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z); return; } float targetSpeed = isRunning ? runSpeed : walkSpeed; if (moveInputVec2.magnitude >= 0.1f) { if (thirdPersonCameraScript == null) return; Vector3 cameraForward = thirdPersonCameraScript.transform.forward; Vector3 cameraRight = thirdPersonCameraScript.transform.right; cameraForward.y = 0; cameraRight.y = 0; cameraForward.Normalize(); cameraRight.Normalize(); Vector3 desiredMoveDirection = (cameraForward * moveInputVec2.y + cameraRight * moveInputVec2.x).normalized; if (desiredMoveDirection != Vector3.zero) { transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), turnSpeed * Time.fixedDeltaTime); } Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed; currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, targetHorizontalVelocity, ref smoothDampVelocity, moveSmoothTime); } else { currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime); } rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z); }
    private IEnumerator DashCoroutine() { isDashing = true; lastDashTime = Time.time; animator.SetTrigger(hashDash); Vector3 moveDirection = new Vector3(moveInputVec2.x, 0, moveInputVec2.y); Vector3 dashDirection = transform.forward; if (moveDirection.magnitude > 0.1f) { if (thirdPersonCameraScript == null) { isDashing = false; yield break; } dashDirection = thirdPersonCameraScript.transform.TransformDirection(moveDirection).normalized; dashDirection.y = 0; } float startTime = Time.time; while (Time.time < startTime + dashDuration) { rb.velocity = new Vector3(dashDirection.x * dashSpeed, 0, dashDirection.z * dashSpeed); yield return new WaitForFixedUpdate(); } float slideStartTime = Time.time; Vector3 slideStartVelocity = rb.velocity; Vector3 finalVelocity = new Vector3(0, rb.velocity.y, 0); while (Time.time < slideStartTime + dashSlideDuration) { float t = (Time.time - slideStartTime) / dashSlideDuration; rb.velocity = Vector3.Lerp(slideStartVelocity, finalVelocity, t); yield return new WaitForFixedUpdate(); } isDashing = false; }
    private void GroundCheck() { if (rb == null) return; CapsuleCollider capCol = GetComponent<CapsuleCollider>(); Vector3 sphereOrigin = transform.position + Vector3.up * (capCol.center.y - capCol.height / 2f + capCol.radius); _isGrounded = Physics.CheckSphere(sphereOrigin, capCol.radius, groundMask); if (_isGrounded) { lastGroundedTime = Time.time; } }
    private void ApplyBetterGravity() { if (rb == null) return; if (rb.velocity.y < 0) { rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime; } else if (rb.velocity.y > 0) { rb.velocity += Vector3.up * Physics.gravity.y * (gravityMultiplier - 1) * Time.fixedDeltaTime; } }
    private void HandleRandomIdle() { if (animator == null) return; if (animator.GetCurrentAnimatorStateInfo(0).IsName("Stop")) { idleTimer += Time.deltaTime; if (idleTimer >= randomIdleWaitTime) { animator.SetTrigger(hashIdleTrigger); ResetIdleTimer(); } } else { ResetIdleTimer(); } }
    private void ResetIdleTimer() { idleTimer = 0f; randomIdleWaitTime = Random.Range(minIdleWaitTime, maxIdleWaitTime); }
}
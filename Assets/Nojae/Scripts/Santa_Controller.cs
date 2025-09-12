using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // <<< 이 줄을 추가하세요.
using UnityEngine.UI; // UI 요소를 사용하기 위해 꼭 추가해주세요!


[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class SantaController : MonoBehaviour, IPunObservable
{
    [Header("카메라 설정")]
    public Transform cameraTransform;

    [Header("움직임 설정")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float animationSmoothTime = 0.2f;

    [Header("액션 설정")]
    public float jumpForce = 5.0f;
    public float jumpCooldown = 1.0f;
    public float punchCooldown = 0.5f;

    [Header("스태미나 설정")]
    [Tooltip("최대 스태미나 수치입니다.")]
    public float maxStamina = 100f;
    [Tooltip("달릴 때 초당 소모되는 스태미나 양입니다.")]
    public float staminaUseRate = 20f;
    [Tooltip("초당 회복되는 스태미나 양입니다.")]
    public float staminaRegenRate = 15f;
    [Tooltip("달리기를 멈춘 후 스태미나 회복이 시작되기까지의 시간입니다.")]
    public float staminaRegenDelay = 1.5f;
    [Tooltip("점프 시 소모되는 고정 스태미나 양입니다.")]
    public float jumpStaminaCost = 15f;

    [Header("시점 변환 설정")]
    public float lookSensitivity = 0.1f;

    [Header("땅 감지 설정")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;


    private Slider staminaBar;
    private GameObject interactionUIGroup; // 상호작용 UI 그룹 (F to Capture 등)
    private GameObject gamestatusUiGroup; // 게임 상태 UI 그룹 (스태미나 바 등)
    private Slider interactionSlider;    // 홀드 진행 바
    private TMP_Text interactionText;      // 상호작용 텍스트

    [Header("포획 및 썰매 참조")]
    public Sleigh sleigh; // Sleigh 스크립트를 직접 연결
    [Tooltip("산타가 들고 다니는 보따리 오브젝트")]
    public GameObject sackPrefab; // Resources 폴더에 있어야 함

    private ReindeerController capturedReindeer; // 내가 현재 포획한 순록


    // --- 내부 변수들 ---
    private PhotonView photonView;
    private PhotonView sackPhotonView; // <--- 2. 보따리의 PhotonView를 저장할 변수
    private IInteractable currentInteractable;
    private Coroutine interactionCoroutine;

    private bool hasCapturedReindeer = false; // [추가] 순록을 포획했는지 여부
    private Rigidbody rb;
    private Animator animator;
    private Santa_Input playerInput;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float xRotation = 0f;
    private bool isGrounded;
    private bool isRunning;
    private bool isTryingToRun; // 사용자가 달리려고 하는지 여부
    private float animationSpeedVelocity;
    private float nextJumpTime = 0f;
    private float nextPunchTime = 0f;
    private float currentStamina;
    private float lastRunTime;

    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundHash = Animator.StringToHash("isGround");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int punchHash = Animator.StringToHash("isPunching_Right");

        /// <summary>
    /// 이 산타가 현재 순록을 포획한 상태인지 여부를 반환합니다.
    /// </summary>
    public bool HasCapturedReindeer()
    {
        return capturedReindeer != null;
    }

    /// <summary>
    /// 현재 포획한 순록의 정보를 반환합니다.
    /// </summary>
    public ReindeerController GetCapturedReindeer()
    {
        return capturedReindeer;
    }

    void Start()
    {
        if (!photonView.IsMine) return;

        // 게임 시작 시 상호작용 UI를 확실하게 끕니다.
        if (interactionUIGroup != null) interactionUIGroup.SetActive(false);
        if (photonView.IsMine && UIManager.instance != null)
        {
            // UIManager로부터 산타용 캔버스를 가져옵니다.
            Transform canvasTransform = UIManager.instance.santaCanvas.transform;

            // 캔버스 안에서 필요한 UI 요소들을 이름으로 직접 찾아옵니다.
            interactionUIGroup = canvasTransform.Find("Interaction_UI_Group")?.gameObject;
            gamestatusUiGroup = canvasTransform.Find("Game_Status_UI_Group")?.gameObject;

            if (interactionUIGroup != null)
            {
                interactionText = interactionUIGroup.transform.Find("Interact_Text")?.GetComponent<TMP_Text>();
                interactionSlider = interactionUIGroup.transform.Find("Interact_Hold_Slider")?.GetComponent<Slider>();
            }
            if (gamestatusUiGroup != null)
            {
                staminaBar = gamestatusUiGroup.transform.Find("Stamina_Slider")?.GetComponent<Slider>();
            }

            // 스태미나 바 초기화
            if (staminaBar != null)
            {
                staminaBar.maxValue = maxStamina;
                staminaBar.value = maxStamina;
            }

            if (interactionUIGroup != null) interactionUIGroup.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Awake()
    {
        photonView = GetComponent<PhotonView>(); // <<< 이 줄 추가
            // --- 내 캐릭터가 아닐 경우 비활성화 ---
    if (!photonView.IsMine)
    {
        if (cameraTransform != null) cameraTransform.gameObject.SetActive(false);
        this.enabled = false;
        return; // return을 추가하여 아래 초기화 코드가 실행되지 않도록 합니다.
    }
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = new Santa_Input();

        // 스태미나 초기화
        currentStamina = maxStamina;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        playerInput.Santa.Enable();
        playerInput.Santa.Move.performed += OnMoveInput;
        playerInput.Santa.Move.canceled += OnMoveInput;
        playerInput.Santa.Punch.performed += OnPunchInput;
        playerInput.Santa.Jump.performed += OnJumpInput;
        playerInput.Santa.Run.performed += OnRunInput;
        playerInput.Santa.Run.canceled += OnRunInput;
        playerInput.Santa.Interact.started += OnInteractionStart;
        playerInput.Santa.Interact.canceled += OnInteractionCancel;
    }

    private void OnDisable()
    {
        playerInput.Santa.Disable();
        playerInput.Santa.Move.performed -= OnMoveInput;
        playerInput.Santa.Move.canceled -= OnMoveInput;

        playerInput.Santa.Punch.performed -= OnPunchInput;
        playerInput.Santa.Jump.performed -= OnJumpInput;
        playerInput.Santa.Run.performed -= OnRunInput;
        playerInput.Santa.Run.canceled -= OnRunInput;
        playerInput.Santa.Interact.started -= OnInteractionStart;
        playerInput.Santa.Interact.canceled -= OnInteractionCancel;
}

    private void Update()
    {
        if (!photonView.IsMine) return;
        HandleLook(); // 시점 처리

        CheckForInteractables(); // 주변 탐색
        UpdateInteractionUI();   // UI 업데이트

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        animator.SetBool(isGroundHash, isGrounded);

        // '달리기' 상태를 스태미나와 입력을 조합하여 최종 결정
        isRunning = isTryingToRun && moveInput.magnitude > 0.1f && currentStamina > 0;
        HandleStamina();
        HandleAnimation();
        UpdateUI();
    }


    // 주변 상호작용 대상을 찾는 함수 (새로 추가)
    private void CheckForInteractables()
    {
        currentInteractable = null;
        // 우선순위 1: 잡은 순록이 있고 썰매가 보이면, 상호작용 대상은 썰매
        if (capturedReindeer != null && sleigh != null)
        {
            if (Vector3.Distance(transform.position, sleigh.transform.position) <= 5f)
            {
                currentInteractable = sleigh;
                return;
            }
        }

        // 우선순위 2: 기절한 순록을 발견하면, 상호작용 대상은 순록
        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 5f))
        {
            if (hit.collider.CompareTag("Reindeer"))
            {
                print("Raycast hit a Reindeer!");
                if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    if (interactable.CanInteract(this.gameObject))
                    {
                        currentInteractable = interactable;
                    }
                }
            }
        }
    }

    private void UpdateInteractionUI()
    {
        bool canInteract = (currentInteractable != null);
        if (interactionUIGroup != null) interactionUIGroup.SetActive(canInteract);

        if (canInteract)
        {
            interactionText.text = currentInteractable.GetInteractMessage(this.gameObject);
            bool isHoldType = currentInteractable.InteractionType == InteractionType.Hold;
            if (interactionSlider != null) interactionSlider.gameObject.SetActive(isHoldType && interactionCoroutine != null);
        }
    }



    // 상호작용을 시작하는 함수 (새로 추가)
    private void OnInteractionStart(InputAction.CallbackContext context)
    {
        // HandleInteraction() 함수의 내용을 이곳으로 통합합니다.

        // 우선순위 1: 잡은 순록이 있고, 썰매와 가까우면 -> 썰매에 묶기
        if (capturedReindeer != null && sleigh != null && currentInteractable is Sleigh)
        {
            (currentInteractable as Sleigh).AttachReindeer(capturedReindeer);
            photonView.RPC("SetSackActiveRPC", RpcTarget.All, false);
            capturedReindeer = null;
            currentInteractable = null; // 상호작용 완료 후 대상 초기화
            return;
        }

        // 우선순위 2: 기절한 순록을 발견하면 -> 포획하기
        if (capturedReindeer == null && currentInteractable is ReindeerController)
        {
            print("Interaction Start with Reindeer");
            ReindeerController reindeer = currentInteractable as ReindeerController;
            if (reindeer != null && reindeer.CurrentState == ReindeerController.PlayerState.Stunned)
            {
                // 홀드 상호작용 시작
                if (interactionCoroutine == null)
                    interactionCoroutine = StartCoroutine(HoldInteractionCoroutine(reindeer));
            }
        }
    }

    // 상호작용을 취소하는 함수 (이벤트 콜백)
    private void OnInteractionCancel(InputAction.CallbackContext context)
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

    // 홀드 상호작용 코루틴 (포획 전용)
    private IEnumerator HoldInteractionCoroutine(ReindeerController targetReindeer)
    {
        if (interactionSlider == null) yield break;

        interactionSlider.gameObject.SetActive(true);
        interactionSlider.value = 0;
        float timer = 0f;

        while (timer < 2f) // 2초 홀드
        {
            timer += Time.deltaTime;
            interactionSlider.value = timer / 2f;
            yield return null;
        }

        interactionSlider.gameObject.SetActive(false);

        // 홀드가 끝나도 여전히 상호작용 대상이 맞는지 한번 더 확인
        print("Hold interaction complete. Current interactable: " + (currentInteractable != null ? currentInteractable.GetType().Name : "null"));
        if ((object)currentInteractable == (object)targetReindeer)
        {
            print("Capturing Reindeer now.");
            CaptureReindeer(targetReindeer);
        }

        currentInteractable = null;
        interactionCoroutine = null;
    }



    // 펀치 함수 (순록 기절시키기)
    private void OnPunchInput(InputAction.CallbackContext context)
    {
        if (Time.time < nextPunchTime) return;

        if (Time.time >= nextPunchTime)
        {
            nextPunchTime = Time.time + punchCooldown;
            animator.SetTrigger(punchHash);

            // [수정] 펀치가 맞았는지 로컬에서 확인하고, 맞았다면 RPC로 모든 클라이언트에게 알림
            RaycastHit hit;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 3f)) // 3f는 펀치 사거리
            {
                PhotonView hitPhotonView = hit.collider.GetComponent<PhotonView>();
                if (hit.collider.CompareTag("Reindeer") && hitPhotonView != null)
                {
                    // 맞은 순록에게 "기절하라"는 신호를 보냄
                    hitPhotonView.RPC("GetStunned", RpcTarget.All);
                }
            }
        }
    }

    [PunRPC]
    private void SetSackActiveRPC(bool isActive)
    {
        if (sackPrefab != null)
        {
            sackPrefab.SetActive(isActive);
        }
    }

    [PunRPC]
    private void SetCaptureStateRPC(bool isCapturing)
    {
        // 모든 클라이언트에서 산타의 포획 상태를 동일하게 설정
        this.hasCapturedReindeer = isCapturing;
    }

    private void CaptureReindeer(ReindeerController reindeer)
    {
        // 1. 모든 클라이언트에게 "내 보따리를 활성화해라" 라고 RPC로 명령
        photonView.RPC("SetSackActiveRPC", RpcTarget.All, true);

        // 2. 순록에게 "나(산타)의 보따리에 잡혀라"고 나의 PhotonView ID를 알려줌
        reindeer.GetComponent<PhotonView>().RPC("GetCaptured", RpcTarget.All, this.photonView.ViewID);

        // [수정] capturedReindeer 변수 대신, hasCapturedReindeer 상태를 동기화하도록 변경
        photonView.RPC("SetCaptureStateRPC", RpcTarget.All, true);

        capturedReindeer = reindeer; // 잡은 순록 기록
    }

    // 스노우볼에 맞았을 때 넉백 RPC
    [PunRPC]
    public void ApplyKnockback(Vector3 direction, float force)
    {
        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        HandleMovement(); // Rigidbody를 이용한 캐릭터 이동을 FixedUpdate에서 담당
    }
    private void OnMoveInput(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnLookInput(InputAction.CallbackContext context) => lookInput = context.ReadValue<Vector2>();
    private void OnRunInput(InputAction.CallbackContext context) => isTryingToRun = context.ReadValueAsButton();

    private void OnJumpInput(InputAction.CallbackContext context)
    {
        // 땅에 있고, 쿨타임이 지났고, 점프에 필요한 스태미나가 충분할 때
        if (isGrounded && Time.time >= nextJumpTime && currentStamina >= jumpStaminaCost)
        {
            nextJumpTime = Time.time + jumpCooldown; // 점프 쿨타임 설정

            currentStamina -= jumpStaminaCost; // 스태미나 소모!

            lastRunTime = Time.time; // 점프 직후 스태미나가 바로 회복되지 않도록 시간 갱신

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger(jumpHash);
        }
    }

    private void HandleStamina()
    {
        if (isRunning) // 현재 달리고 있다면
        {
            // 스태미나 소모
            currentStamina -= staminaUseRate * Time.deltaTime;
            if (currentStamina < 0) currentStamina = 0;
            lastRunTime = Time.time; // 마지막으로 달린 시간을 계속 갱신
        }
        else // 달리고 있지 않다면
        {
            // 달리기를 멈추고 일정 시간이 지난 후에만 회복 시작
            if (Time.time > lastRunTime + staminaRegenDelay)
            {
                if (currentStamina < maxStamina)
                {
                    currentStamina += staminaRegenRate * Time.deltaTime;
                    if (currentStamina > maxStamina) currentStamina = maxStamina;
                }
            }
        }
    }

    private void HandleAnimation()
    {
        float targetSpeed = 0f;
        if (moveInput.magnitude > 0.1f)
        {
            // isRunning이 true일 때만 달리기(2) 애니메이션, 아니면 걷기(1)
            targetSpeed = isRunning ? 2.0f : 1.0f;
        }

        float currentSpeed = animator.GetFloat(speedHash);
        float smoothedSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref animationSpeedVelocity, animationSmoothTime);

        animator.SetFloat(speedHash, smoothedSpeed);
    }

    private void HandleMovement()
    {
        float currentMoveSpeed = isRunning ? runSpeed : walkSpeed;

        // 캐릭터가 현재 바라보는 방향(마우스로 회전된 방향)을 기준으로 이동 벡터를 계산합니다.
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        // 물리력을 사용하여 이동합니다.
        rb.velocity = new Vector3(moveDirection.normalized.x * currentMoveSpeed, rb.velocity.y, moveDirection.normalized.z * currentMoveSpeed);

        // <<<<< 캐릭터를 회전시키는 Slerp 로직을 여기서 완전히 제거했습니다. >>>>>
    }

    private void HandleLook()
    {
        // 함수 안에서 직접 Look 액션의 현재 값을 읽어옵니다.
        Vector2 currentLookInput = playerInput.Santa.Look.ReadValue<Vector2>();

        // --- [수정된 부분] ---
        // Time.deltaTime을 다시 곱해서 프레임에 관계없이 부드러운 속도를 보장합니다.
        float mouseX = currentLookInput.x * lookSensitivity * Time.deltaTime;
        float mouseY = currentLookInput.y * lookSensitivity * Time.deltaTime;
        // --- [수정 끝] ---

        // 위아래(Pitch) 시점 변환: 카메라만 회전시킵니다.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 좌우(Yaw) 시점 변환: 캐릭터 몸 전체를 회전시킵니다.
        transform.Rotate(Vector3.up * mouseX);
    }

    private void UpdateUI()
    {
        // staminaBar가 연결되어 있을 때만 값 업데이트
        if (staminaBar != null)
        {
            staminaBar.value = currentStamina;
        }
    }

    public int GetSackViewID()
    {
        // sackPhotonView가 null이 아니면 ViewID를, null이면 0을 반환
        return (sackPhotonView != null) ? sackPhotonView.ViewID : 0;
    }


    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isRunning);
        }
        else
        {
            this.isRunning = (bool)stream.ReceiveNext();
        }
    }

}

using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // UI 요소를 사용하기 위해 꼭 추가해주세요!

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class SantaController : MonoBehaviour
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

    [Header("UI 설정")]
    [Tooltip("스태미나를 표시할 UI 슬라이더를 연결해주세요.")]
    public Slider staminaBar;

    [Header("포획 및 썰매 참조")]
    public Sleigh sleigh; // Sleigh 스크립트를 직접 연결
    [Tooltip("산타가 들고 다니는 보따리 오브젝트")]
    public GameObject sackPrefab; // Resources 폴더에 있어야 함

    private ReindeerController capturedReindeer; // 내가 현재 포획한 순록


    // --- 내부 변수들 ---
    private PhotonView photonView;
    private PhotonView sackPhotonView; // <--- 2. 보따리의 PhotonView를 저장할 변수
    private IInteractable currentInteractable;

    // --- 내부 변수들 ---
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
        // UI 슬라이더 초기 설정
        if (staminaBar != null)
        {
            staminaBar.maxValue = maxStamina;
            staminaBar.value = maxStamina;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        playerInput.Santa.Enable();
        playerInput.Santa.Move.performed += OnMoveInput;
        playerInput.Santa.Move.canceled += OnMoveInput;
        playerInput.Santa.Look.performed += OnLookInput;
        playerInput.Santa.Look.canceled += OnLookInput;
        playerInput.Santa.Punch.performed += OnPunchInput;
        playerInput.Santa.Jump.performed += OnJumpInput;
        playerInput.Santa.Run.performed += OnRunInput;
        playerInput.Santa.Run.canceled += OnRunInput;
    }

    private void OnDisable()
    {
        playerInput.Santa.Disable();
        // ... (이벤트 해제 코드들은 이전과 동일)
    }

    private void Update()
    {
        if (photonView.IsMine)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            animator.SetBool(isGroundHash, isGrounded);

            // '달리기' 상태를 스태미나와 입력을 조합하여 최종 결정
            isRunning = isTryingToRun && moveInput.magnitude > 0.1f && currentStamina > 0;

            HandleStamina();
            HandleAnimation();
            HandleLook();
            UpdateUI();

            // 상호작용 키(F) 입력 처리
            if (playerInput.Santa.Interact.triggered) // Input System 사용
            {
                HandleInteraction();
            }
        }
    }

    // 상호작용 로직을 처리할 새로운 함수
    private void HandleInteraction()
    {
        // 우선순위 1: 잡고 있는 순록이 있고, 썰매와 가까우면 -> 썰매에 묶기
        if (capturedReindeer != null && sleigh != null)
        {
            float distanceToSleigh = Vector3.Distance(transform.position, sleigh.transform.position);
            if (distanceToSleigh <= 5f) // 상호작용 거리
            {
                sleigh.AttachReindeer(capturedReindeer);
                capturedReindeer = null; // 썰매에 넘겼으므로 초기화
                return;
            }
        }

        // 우선순위 2: 기절한 순록을 발견하면 -> 포획하기
        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 5f))
        {
            if (hit.collider.CompareTag("Reindeer"))
            {
                ReindeerController reindeer = hit.collider.GetComponent<ReindeerController>();
                if (reindeer != null && reindeer.CurrentState == ReindeerController.PlayerState.Stunned)
                {
                    CaptureReindeer(reindeer);
                }
            }
        }
    }



    // 펀치 함수 (순록 기절시키기)
    private void OnPunchInput(InputAction.CallbackContext context)
    {
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

    private void CaptureReindeer(ReindeerController reindeer)
    {
        // 1. 모든 클라이언트에게 "내 보따리를 활성화해라" 라고 RPC로 명령
        photonView.RPC("SetSackActiveRPC", RpcTarget.All, true);

        // 2. 순록에게 "나(산타)의 보따리에 잡혀라"고 나의 PhotonView ID를 알려줌
        reindeer.GetComponent<PhotonView>().RPC("GetCaptured", RpcTarget.All, this.photonView.ViewID);

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
        HandleMovement();
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
        // isRunning이 true일 때만 달리기 속도 적용
        float currentMoveSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        rb.velocity = new Vector3(moveDirection.normalized.x * currentMoveSpeed, rb.velocity.y, moveDirection.normalized.z * currentMoveSpeed);
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;
        xRotation = Mathf.Clamp(xRotation - mouseY, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
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
}
// SantaController.cs 상단에 using 추가
using Photon.Pun;


using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // UI 요소를 사용하기 위해 꼭 추가해주세요!

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
// 클래스 선언부에 PhotonView 추가
[RequireComponent(typeof(PhotonView))]
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

    [Header("상호작용 설정")]
    [Tooltip("상호작용을 감지할 최대 거리입니다.")]
    public float interactionDistance = 3.0f;
    [Tooltip("상호작용 가능한 오브젝트들의 레이어입니다.")]
    public LayerMask interactableLayer;
    [Tooltip("상호작용 UI 텍스트를 연결해주세요.")]
    public Text interactionPromptUI; // 간단한 Text 예시, TMPro 사용 시 TextMeshProUGUI로 변경

    [Header("UI 설정")]
    [Tooltip("스태미나를 표시할 UI 슬라이더를 연결해주세요.")]
    public Slider staminaBar;


    [Header("펀치 판정 설정")]
    public float punchRange = 1.5f;
    public float punchRadius = 0.5f;
    public LayerMask reindeerLayer; // 순록 오브젝트의 레이어를 지정해야 합니다.

    [Header("포획 및 썰매 참조")]
    [Tooltip("산타가 들고 다니는 보따리 오브젝트")]
    public GameObject sackObject;

    // --- 내부 변수들 ---
    private PhotonView photonView;
    private PhotonView sackPhotonView;
    private IInteractable currentInteractable; // 현재 바라보고 있는 상호작용 가능 객체

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
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = new Santa_Input();
        photonView = GetComponent<PhotonView>(); // PhotonView 컴포넌트 가져오기

        // 내 캐릭터가 아니면 컨트롤러 비활성화
        if (!photonView.IsMine)
        {
            cameraTransform.gameObject.SetActive(false);
            GetComponent<PlayerInput>().enabled = false; // PlayerInput 컴포넌트가 있다면 비활성화
            enabled = false;
            return;
        }

        // 보따리의 PhotonView 미리 찾아두기
        if (sackObject != null)
        {
            sackPhotonView = sackObject.GetComponent<PhotonView>();
            if (sackPhotonView == null)
            {
                Debug.LogError("보따리에 PhotonView 컴포넌트가 없습니다!");
            }
        }

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
        playerInput.Santa.Interact.performed += OnInteractInput;
    }

    private void OnDisable()
    {
        playerInput.Santa.Disable();
        playerInput.Santa.Move.performed -= OnMoveInput;
        playerInput.Santa.Move.canceled -= OnMoveInput;
        playerInput.Santa.Look.performed -= OnLookInput;
        playerInput.Santa.Look.canceled -= OnLookInput;
        playerInput.Santa.Punch.performed -= OnPunchInput;
        playerInput.Santa.Jump.performed -= OnJumpInput;
        playerInput.Santa.Run.performed -= OnRunInput;
        playerInput.Santa.Run.canceled -= OnRunInput;
        playerInput.Santa.Interact.performed -= OnInteractInput;
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        animator.SetBool(isGroundHash, isGrounded);

        // '달리기' 상태를 스태미나와 입력을 조합하여 최종 결정
        isRunning = isTryingToRun && moveInput.magnitude > 0.1f && currentStamina > 0;

        HandleStamina();
        HandleAnimation();
        HandleLook();
        HandleInteractionCheck(); // 상호작용 탐지 함수 호출
        UpdateUI();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void OnMoveInput(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnLookInput(InputAction.CallbackContext context) => lookInput = context.ReadValue<Vector2>();
    private void OnRunInput(InputAction.CallbackContext context) => isTryingToRun = context.ReadValueAsButton();

    private void OnPunchInput(InputAction.CallbackContext context)
    {
        if (Time.time >= nextPunchTime)
        {
            nextPunchTime = Time.time + punchCooldown;
            animator.SetTrigger(punchHash);
        }

        // 펀치 판정 로직
        RaycastHit hit;
        Vector3 startPoint = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;

        // SphereCast로 전방 원뿔 형태로 판정
        if (Physics.SphereCast(startPoint, punchRadius, direction, out hit, punchRange, reindeerLayer))
        {
            ReindeerController reindeer = hit.collider.GetComponent<ReindeerController>();
            if (reindeer != null)
            {
                Debug.Log(reindeer.name + " 명중!");
                PhotonView reindeerView = reindeer.GetComponent<PhotonView>();
                if (reindeerView != null)
                {
                    // 순록에게 GetStunned RPC 호출
                    reindeerView.RPC("GetStunned", RpcTarget.All);
                }
            }
        }
    }


    // 상호작용 키 입력 처리
    private void OnInteractInput(InputAction.CallbackContext context)
    {
        if (currentInteractable != null && currentInteractable.CanInteract)
        {
            // 상호작용 실행, '나 자신(산타)'의 게임오브젝트를 넘겨줌
            currentInteractable.Interact(this.gameObject);
        }
    }

    // 매 프레임 상호작용 가능한 객체 탐지
    private void HandleInteractionCheck()
    {
        RaycastHit hit;
        // 카메라 중앙에서 레이를 쏨
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, interactionDistance, interactableLayer))
        {
            // IInteractable 인터페이스를 가진 컴포넌트를 찾음
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null && interactable.CanInteract)
            {
                // 상호작용 가능한 객체를 찾았을 때
                currentInteractable = interactable;
                if (interactionPromptUI != null)
                {
                    interactionPromptUI.text = currentInteractable.GetInteractMessage(this.gameObject);
                    interactionPromptUI.gameObject.SetActive(true);
                }
            }
            else
            {
                // 상호작용 불가능한 객체이거나, 조건이 맞지 않을 때
                ClearInteraction();
            }
        }
        else
        {
            // 레이에 아무것도 맞지 않았을 때
            ClearInteraction();
        }
    }

    // 상호작용 정보 초기화
    private void ClearInteraction()
    {
        currentInteractable = null;
        if (interactionPromptUI != null)
        {
            interactionPromptUI.gameObject.SetActive(false);
        }
    }

    // 다른 스크립트에서 산타의 보따리 ID를 가져갈 수 있도록 public 함수 추가
    public int GetSackViewID()
    {
        return (sackPhotonView != null) ? sackPhotonView.ViewID : 0;
    }

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
}
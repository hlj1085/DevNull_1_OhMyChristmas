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

    [Header("아이템 장착")]
    [Tooltip("아이템을 물었을 때 붙을 위치 (예: 입 주변의 빈 오브젝트)")]
    public Transform mouthAttachPoint;

    // --- 비공개 로직 변수 ---
    private ItemData equippedItem; // 현재 장착한 아이템 데이터
    private GameObject equippedItemObject; // 현재 장착해서 입에 물고 있는 아이템의 3D 모델

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
    [Tooltip("땅을 감지할 구체의 반지름입니다.")]
    public float groundCheckRadius = 0.4f;
    [Tooltip("캐릭터의 중심에서 아래로 스피어캐스트를 쏠 최대 거리입니다.")]
    public float groundCheckDistance = 0.6f;
    public float gravityMultiplier = 2.5f;
    public float fallMultiplier = 5f;
    public LayerMask groundMask;

    // 외부에서 현재 상태를 읽기 위한 프로퍼티
    public PlayerState CurrentState => currentState;
    public bool IsMoving => moveInputVec2.magnitude > 0.1f;

    // --- UI 참조 변수 (UIManager를 통해 할당됨) ---
    private GameObject interactionUIGroup;
    private GameObject recoveryUIGroup;
    private TextMeshProUGUI useItemPromptUI; // <<< [추가] 아이템 사용 안내 텍스트
    private TextMeshProUGUI interactionPromptUI;
    private Slider interactionSlider;
    private Slider recoverySlider;

    // --- 내부 로직 변수 ---
    private int lastEquippedSlot = -1; // 마지막으로 장착한 아이템 슬롯 번호 (-1은 없음)

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
                useItemPromptUI = UIManager.instance.useItemPromptUI; // <<< [추가]

                // 게임 시작 시 UI들을 확실하게 꺼줍니다.
                if (interactionUIGroup != null) interactionUIGroup.SetActive(false);
                if (recoveryUIGroup != null) recoveryUIGroup.SetActive(false);
                if (useItemPromptUI != null) useItemPromptUI.gameObject.SetActive(false); // <<< [추가]

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

        // --- [핵심 수정] ---
        // 매 물리 프레임 시작 시, 일단 공중에 떠있다고 가정합니다.
        // OnCollisionStay에서 땅과 닿는 것이 확인되면 이 값은 즉시 true로 바뀝니다.
        _isGrounded = false;

        // GroundCheck() 함수 호출은 삭제합니다.

        if (isDashing) return;

        ApplyMovement();
        ApplyBetterGravity();
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            photonView.RPC("GetStunned", RpcTarget.All);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5) && currentState == PlayerState.Stunned)
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

        // --- 1. 기절 또는 포획 상태 처리 ---
        if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
        {
            recoveryUIGroup.SetActive(true);
            interactionUIGroup.SetActive(false); // 상호작용 UI는 확실히 끈다.
            if (recoverySlider != null)
            {
                recoverySlider.value = currentRecoveryTimer / recoveryTime;
            }
            return; // 다른 UI 로직을 실행할 필요가 없으므로 여기서 종료
        }

        // --- 2. 정상 상태 처리 ---

        // 회복 UI는 반드시 끈다.
        recoveryUIGroup.SetActive(false);

        // --- [핵심 수정] ---
        // 현재 상호작용 대상이 유효한지 먼저 확인합니다.
        // 대상이 파괴되었거나(Unity의 null 체크), CanInteract가 false가 되었다면,
        if (currentInteractable == null || !currentInteractable.CanInteract)
        {
            // UI를 끄고, 더 이상 상호작용 대상이 없다고 명확히 합니다.
            if (interactionUIGroup.activeSelf)
            {
                interactionUIGroup.SetActive(false);
            }
            // OnTriggerExit이 호출되기 전이라도, 더 이상 유효하지 않으므로 참조를 제거합니다.
            if (currentInteractable != null && !currentInteractable.CanInteract)
            {
                currentInteractable = null;
            }
            return; // UI를 껐으므로 더 이상 진행할 필요 없음
        }

        // 위 관문을 통과했다면, 상호작용이 가능한 상태이므로 UI를 켭니다.
        interactionUIGroup.SetActive(true);

        // 세부 UI 설정 (텍스트, 홀드 슬라이더 등)
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
    // Interact 함수의 반환 타입을 bool로 변경하고, true를 return
    public bool Interact(Inventory interactorInventory)
    {
        if (photonView != null)
        {
            photonView.RPC("GetRescued", RpcTarget.All);
        }
        return true; // 구출 시도에 성공했으므로 true 반환
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
        // --- [추가] 아이템 관련 입력 ---
        inputActions.Player.Item1.performed += _ => EquipItemFromSlot(0); // 1번 키
        inputActions.Player.Item2.performed += _ => EquipItemFromSlot(1); // 2번 키
        inputActions.Player.Item3.performed += _ => EquipItemFromSlot(2); // 3번 키
        inputActions.Player.UseItem.performed += _ => UseEquippedItem();   // 사용 키

    }

    private void EquipItemFromSlot(int slotIndex)
    {
        var items = inventory.GetItems();

        // 1. 누른 슬롯이 비어있다면 -> 무조건 맨손으로
        if (slotIndex >= items.Count || items[slotIndex] == null)
        {
            UnequipItem();
            return;
        }

        // --- [핵심 수정] ---
        // 2. 만약 현재 아이템을 들고 있고(맨손이 아니고), 방금 누른 슬롯이 그 아이템의 슬롯과 같다면 -> 맨손으로
        if (equippedItem != null && slotIndex == lastEquippedSlot)
        {
            UnequipItem();
            return;
        }

        // 3. 위 두 경우가 아니라면 (다른 아이템을 선택했거나, 맨손에서 아이템을 선택했다면) -> 새로운 아이템 장착/교체
        ItemData newItem = items[slotIndex];

        // 이전에 물고 있던 아이템 모델 제거
        if (equippedItemObject != null)
        {
            Destroy(equippedItemObject);
        }

        // 새 아이템 정보로 업데이트
        equippedItem = newItem;
        lastEquippedSlot = slotIndex; // 마지막으로 장착한 슬롯 번호 기억

        // 새 아이템 모델 생성
        if (equippedItem.itemPrefab != null)
        {
            equippedItemObject = Instantiate(equippedItem.itemPrefab, mouthAttachPoint);
        }

        // UI 업데이트
        if (UIManager.instance != null)
        {
            UIManager.instance.inventoryUI.UpdateSelection(slotIndex);
        }
        if (useItemPromptUI != null)
        {
            useItemPromptUI.text = "E키를 눌러 사용하기";
            useItemPromptUI.gameObject.SetActive(true);
        }
    }


    // 장착한 아이템을 사용하는 함수
    private void UseEquippedItem()
    {
        // 1. 장착한 아이템이 없으면 아무것도 하지 않음
        if (equippedItem == null) return;

        Debug.Log(equippedItem.itemName + " 아이템 사용!");

        // 2. 아이템 데이터에게 효과를 실행하라고 먼저 명령
        equippedItem.Use(this);

        // 3. 사용한 아이템은 인벤토리에서 제거
        inventory.RemoveItem(equippedItem);

        // 4. 아이템을 사용했으므로, 장착 해제 함수를 호출하여 모든 상태를 깔끔하게 정리
        UnequipItem();
    }
    /// <summary>
    /// 현재 장착한 아이템을 해제하고 맨손 상태로 돌아갑니다.
    /// </summary>
    private void UnequipItem()
    {
        // 이미 맨손 상태이면 아무것도 하지 않음
        if (equippedItem == null) return;

        Debug.Log("아이템 장착 해제 (맨손 상태)");

        // 입에 물고 있던 3D 모델이 있다면 파괴
        if (equippedItemObject != null)
        {
            Destroy(equippedItemObject);
        }

        // 모든 관련 변수를 깨끗하게 초기화
        equippedItem = null;
        equippedItemObject = null;
        lastEquippedSlot = -1; // 마지막 슬롯 기록도 초기화

        // UI 선택 테두리를 모두 끔
        if (UIManager.instance != null && UIManager.instance.inventoryUI != null)
        {
            UIManager.instance.inventoryUI.UpdateSelection(-1);
        }

        // "사용하기" 안내 텍스트를 숨김
        if (useItemPromptUI != null)
        {
            useItemPromptUI.gameObject.SetActive(false);
        }
    }
    // --- 아이템 효과 RPC 함수들 ---

    [PunRPC]
    public void ApplySpeedBoost(float newSpeed, float duration) // private -> public으로 변경
    {
        StartCoroutine(SpeedBoostCoroutine(newSpeed, duration));
    }

    public void ThrowItem(string prefabName, float force)
    {
        photonView.RPC("ThrowItemRPC", RpcTarget.All, prefabName, force);
    }

    private IEnumerator SpeedBoostCoroutine(float newSpeed, float duration)
    {
        float originalSpeed = runSpeed;
        runSpeed = newSpeed;
        Debug.Log("속도 증가! 현재 속도: " + runSpeed);

        yield return new WaitForSeconds(duration);

        runSpeed = originalSpeed;
        Debug.Log("속도 원래대로 복귀. 현재 속도: " + runSpeed);
    }

    [PunRPC]
    private void ThrowItemRPC(string projectilePrefabName, float force)
    {
        // 던지는 로직은 모든 클라이언트에서 실행되어야 모두에게 보임
        // 카메라 방향은 로컬 플레이어 기준으로 계산
        Vector3 throwDirection = thirdPersonCameraScript.transform.forward;
        Vector3 spawnPosition = mouthAttachPoint.position + throwDirection * 0.5f;

        // 마스터 클라이언트만 생성하여 중복 방지
        if (PhotonNetwork.IsMasterClient)
        {
            GameObject projectile = PhotonNetwork.Instantiate(projectilePrefabName, spawnPosition, Quaternion.LookRotation(throwDirection));
            if (projectile.GetComponent<Rigidbody>() != null)
            {
                projectile.GetComponent<Rigidbody>().AddForce(throwDirection * force, ForceMode.VelocityChange);
            }
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
        if (currentInteractable == null || !currentInteractable.CanInteract || CurrentState != PlayerState.Normal) return;

        // 즉시 발동 아이템 (아이템 줍기 등)
        if (currentInteractable.InteractionType == InteractionType.Instant)
        {
            // 상호작용을 시도하고, 그 결과를 'success' 변수에 저장
            bool success = currentInteractable.Interact(inventory);

            // [핵심] 상호작용에 성공했다면, 즉시 대상을 잊어버린다!
            if (success)
            {
                currentInteractable = null;
            }
        }
        // 홀드 상호작용 (상자 열기, 구출 등)
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
    private void ApplyBetterGravity() { if (rb == null) return; if (rb.velocity.y < 0) { rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime; } else if (rb.velocity.y > 0) { rb.velocity += Vector3.up * Physics.gravity.y * (gravityMultiplier - 1) * Time.fixedDeltaTime; } }
    private void HandleRandomIdle() { if (animator == null) return; if (animator.GetCurrentAnimatorStateInfo(0).IsName("Stop")) { idleTimer += Time.deltaTime; if (idleTimer >= randomIdleWaitTime) { animator.SetTrigger(hashIdleTrigger); ResetIdleTimer(); } } else { ResetIdleTimer(); } }
    private void ResetIdleTimer() { idleTimer = 0f; randomIdleWaitTime = Random.Range(minIdleWaitTime, maxIdleWaitTime); }

    /// <summary>
    /// 이 캐릭터의 콜라이더가 다른 콜라이더와 닿아있는 동안 계속 호출되는 함수입니다.
    /// </summary>
    private void OnCollisionStay(Collision collision)
    {
        // 닿은 상대방의 레이어가 groundMask에 포함되어 있는지 확인합니다.
        // (collision.gameObject.layer는 숫자, groundMask.value는 비트마스크 값이므로 비트 연산으로 확인)
        if ((groundMask.value & (1 << collision.gameObject.layer)) > 0)
        {
            // 닿고 있다면 땅 위에 있는 것으로 판정합니다.
            _isGrounded = true;
            lastGroundedTime = Time.time;
        }
    }
}
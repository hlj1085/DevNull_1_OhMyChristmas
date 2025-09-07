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
        // 플레이어의 현재 상태를 정의합니다. 상태정의
        public enum PlayerState
        {
            Normal,
            Stunned,
            Captured,
            TiedToSleigh,
            PermanentlyTied // <<< [추가] 구출 불가 상태
        }

        [Header("참조")]
        [Tooltip("요정 가루 Item Data 연결")]
        public ItemData fairyDustItemData; // <<< [추가] 요정 가루 ItemData 연결
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

        private bool isAttachedToSleigh = false;

        // 외부에서 isAttachedToSleigh 상태를 읽기 위한 public 프로퍼티
        public bool IsAttachedToSleigh => isAttachedToSleigh;

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
        private TextMeshProUGUI recoveryText; // <<< [추가]
        private Image recoverySliderFill;     // <<< [추가]

        // --- 내부 로직 변수 ---
        private int lastEquippedSlot = -1; // 마지막으로 장착한 아이템 슬롯 번호 (-1은 없음)
        private int tieCount = 0; // 썰매에 묶인 횟수

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
                    recoveryText = UIManager.instance.recoveryText;         // <<< [추가]
                    recoverySliderFill = UIManager.instance.recoverySliderFill; // <<< [추가]

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
            if (isAttachedToSleigh) return;
            if (currentState == PlayerState.TiedToSleigh) return;

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
            if (equippedItem != null)
            {
                // 즉시 장착을 해제합니다. (맨손으로 만듦)
                UnequipItem();
            }
            if (currentState != PlayerState.Normal) return;
            currentState = PlayerState.Stunned;
            currentRecoveryTimer = 0f;
            lastMashTime = -mashCooldown;
            animator.SetTrigger(hashStun);
        }

        [PunRPC]
        public void GetCaptured(int santaViewID)
        {   
            if (currentState != PlayerState.Stunned) return;

            PhotonView santaPhotonView = PhotonView.Find(santaViewID);
            if (santaPhotonView == null)
            {
                Debug.LogError("포획 RPC 오류: ID " + santaViewID + "의 산타를 찾을 수 없습니다!");
                return;
            }

            // SantaController에서 보따리 오브젝트를 직접 찾아 연결
            SantaController santa = santaPhotonView.GetComponent<SantaController>();
            if (santa != null && santa.sackPrefab != null)
            {
                currentSackTransform = santa.sackPrefab.transform;
            }
            currentSackTransform = santaPhotonView.transform;
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
                if (currentState == PlayerState.Captured || currentState == PlayerState.TiedToSleigh)
                {
                    ReleaseFromCapture();
                }
                currentState = PlayerState.Normal;
            }
        }

        [PunRPC]
        public void ReleaseFromCapture()
        {
            // 썰매에 묶여있었다면 연결을 해제합니다.
            if (isAttachedToSleigh)
            {
                transform.SetParent(null);
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
                isAttachedToSleigh = false;
            }
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
            // UI 컴포넌트가 없으면 즉시 종료 (안전장치)
            if (recoveryUIGroup == null || interactionUIGroup == null || useItemPromptUI == null) return;

            // --- 1. 기절 또는 포획 상태일 때 ---
            if (currentState == PlayerState.Stunned || currentState == PlayerState.Captured)
            {
                // 회복 UI만 켜고 나머지 UI는 모두 끈다.
                recoveryUIGroup.SetActive(true);
                interactionUIGroup.SetActive(false);
                useItemPromptUI.gameObject.SetActive(false);

                if (recoverySlider != null)
                {
                    recoverySlider.value = currentRecoveryTimer / recoveryTime;
                }
                if (currentState == PlayerState.Stunned)
                {
                    if (recoveryText != null) recoveryText.text = "Struggle - Press E";
                    if (recoverySliderFill != null) recoverySliderFill.color = Color.green;
                }
                else // PlayerState.Captured
                {
                    if (recoveryText != null) recoveryText.text = "CAPTURED";
                    if (recoverySliderFill != null) recoverySliderFill.color = Color.red;
                }
            }
            // --- 2. 정상 상태일 때 ---
            else if (currentState == PlayerState.Normal)
            {
                // 회복 UI는 반드시 끈다.
                recoveryUIGroup.SetActive(false);

                // 우선순위 1: 장착한 아이템이 있는지 먼저 확인
                if (equippedItem != null)
                {
                    // 장착한 아이템이 사용 불가능한 '퀘스트' 아이템인지 확인합니다.
                    bool isQuestItem = (equippedItem.effects != null && equippedItem.effects.Count > 0 && equippedItem.effects[0] is QuestItemEffect);

                    // 퀘스트 아이템이 아닐 경우에만 "사용하기" 안내 UI를 켭니다.
                    if (!isQuestItem)
                    {
                        useItemPromptUI.gameObject.SetActive(true);
                        useItemPromptUI.text = "Press E to use";
                    }
                    else // 퀘스트 아이템이라면 UI를 끕니다.
                    {
                        useItemPromptUI.gameObject.SetActive(false);
                    }

                    // 일반 상호작용 UI는 반드시 끈다.
                    interactionUIGroup.SetActive(false);
                }
                // 우선순위 2: 장착한 아이템이 없을 때만 주변 상호작용을 확인
                else
                {
                    // '사용' 안내 UI는 반드시 끈다.
                    useItemPromptUI.gameObject.SetActive(false);

                    // 기존의 주변 상호작용 가능 여부를 체크하는 로직 실행
                    bool canInteract = (currentInteractable != null && currentInteractable.CanInteract);
                    interactionUIGroup.SetActive(canInteract);

                    if (canInteract)
                    {
                        // 세부 UI 설정 (텍스트, 홀드 슬라이더 등)
                        if (interactionPromptUI != null)
                            interactionPromptUI.text = currentInteractable.GetInteractMessage(this.gameObject);

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
            }
            // --- 3. 그 외 모든 경우 ---
            else
            {
                // 모든 UI를 끈다.
                recoveryUIGroup.SetActive(false);
                interactionUIGroup.SetActive(false);
                useItemPromptUI.gameObject.SetActive(false);
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
                stream.SendNext(tieCount); // 나의 묶인 횟수를 보냄
            }
            else
            {
                this.currentState = (PlayerState)stream.ReceiveNext();
                this.currentRecoveryTimer = (float)stream.ReceiveNext();
                this.tieCount = (int)stream.ReceiveNext(); // 다른 사람의 묶인 횟수를 받음
            }
        }

        // --- IInteractable (다른 플레이어가 구출/아이템 받기) ---

        // 상호작용 타입은 항상 홀드
        public InteractionType InteractionType => InteractionType.Hold;

        // 상호작용 가능 조건
        public bool CanInteract
        {
            get
            {
                if (currentState == PlayerState.Stunned || currentState == PlayerState.TiedToSleigh)
                    return true;    
                // '구출 불가' 상태일 때는, 요정 가루를 가지고 있을 때만 상호작용 가능
                if (currentState == PlayerState.PermanentlyTied)
                    return inventory.HasItem(fairyDustItemData);
                return false;
            }
        }

        public string GetInteractMessage(GameObject interactorObject)
        {
            // 상호작용한 오브젝트의 태그를 확인
            if (interactorObject.CompareTag("Santa"))
            {
                // 산타가 보고 있을 때
                if (currentState == PlayerState.Stunned)
                {
                    return "Press F for Capture Reindeer";
                }
            }
            else if (interactorObject.CompareTag("Reindeer"))
            {
                if (currentState == PlayerState.Stunned) return "F to Help Reindeer";
                if (currentState == PlayerState.TiedToSleigh) return "F to Untie Reindeer";
                if (currentState == PlayerState.PermanentlyTied)
                {
                    // 요정 가루가 있을 때만 아이템 전달 메시지를 보여줌
                    return inventory.HasItem(fairyDustItemData) ? "F to Take Fairy Dust" : "";
                }
            }
            return "";
        }
        public bool Interact(GameObject interactorObject)
        {
            if (photonView == null) return false;

            // --- 상호작용한 대상이 '산타'일 경우 ---
            if (interactorObject.CompareTag("Santa"))
            {
                // 산타는 '기절' 상태의 순록만 포획할 수 있습니다.
                if (currentState == PlayerState.Stunned)
                {
                    SantaController santa = interactorObject.GetComponent<SantaController>();
                    if (santa != null)
                    {
                        int sackId = santa.GetSackViewID();
                        if (sackId != 0)
                        {
                            // 포획 RPC 호출
                            photonView.RPC("GetCaptured", RpcTarget.All, sackId);
                            return true; // 상호작용 성공
                        }
                }
                }
            }
            // --- 상호작용한 대상이 다른 '순록'일 경우 ---
            else if (interactorObject.CompareTag("Reindeer"))
            {
                switch (currentState)
                {
                    // '기절' 또는 '썰매에 묶인' 상태의 동료를 구출합니다.
                    case PlayerState.Stunned:
                    case PlayerState.TiedToSleigh:
                        photonView.RPC("GetRescued", RpcTarget.All);
                        return true; // 상호작용 성공

                    // '영구적으로 묶인' 상태의 동료에게 아이템을 받습니다.
                    case PlayerState.PermanentlyTied:
                        Inventory interactorInventory = interactorObject.GetComponent<Inventory>();
                        PhotonView interactorView = interactorObject.GetComponent<PhotonView>();

                        // 아이템 이전 로직 (기존 코드와 동일)
                        if (interactorView != null && inventory.HasItem(fairyDustItemData))
                        {
                            this.photonView.RPC("RemoveItemByNameRPC", RpcTarget.All, fairyDustItemData.name);
                            interactorView.RPC("AddItemByNameRPC", RpcTarget.All, fairyDustItemData.name);
                            return true; // 상호작용 성공
                        }
                        break;
                }
            }

            // 위 조건에 해당하지 않으면 상호작용 실패
            return false;
        }

        [PunRPC]
        private void AddItemByNameRPC(string itemName)
        {
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            if (itemData != null)
            {
                inventory.AddItem(itemData);
            }
        }

        [PunRPC]
        private void RemoveItemByNameRPC(string itemName)
        {
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            if (itemData != null)
            {
                inventory.RemoveItem(itemData);
            }
        }

        // 썰매, 묶기
        [PunRPC]
        public void AttachToSleigh(int sleighViewID, int slotIndex)
        {
            if (isAttachedToSleigh || currentState != PlayerState.Captured) return;

            PhotonView sleighPhotonView = PhotonView.Find(sleighViewID);
            Sleigh sleigh = sleighPhotonView.GetComponent<Sleigh>();
            if (sleigh == null || slotIndex >= sleigh.attachmentPoints.Length) return;
            tieCount++;
            Debug.Log(this.name + "이(가) " + tieCount + "번째 묶였습니다.");

            // --- [핵심 수정] ---
            // 묶인 횟수에 따라 상태를 다르게 결정
            if (tieCount >= 3)
            {
                currentState = PlayerState.PermanentlyTied; // 3번째는 '구출 불가' 상태
            }
            else
            {
                currentState = PlayerState.TiedToSleigh; // 1, 2번째는 '구출 가능'한 묶임 상태
            }

            if (currentSackTransform != null)
            {
                currentSackTransform.gameObject.SetActive(false);
            }

            if (reindeerVisuals != null)
            {
                reindeerVisuals.SetActive(true);
            }

            if (photonView.IsMine && thirdPersonCameraScript != null)
            {
                thirdPersonCameraScript.target = this.transform;
            }

            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // 부모 설정 및 위치 고정
            Transform attachPoint = sleigh.attachmentPoints[slotIndex];
            transform.SetParent(attachPoint);
            transform.position = attachPoint.position;
            transform.rotation = attachPoint.rotation;
            transform.localScale = Vector3.one;

            isAttachedToSleigh = true;
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

        // --- 아이템 장착 동기화를 위한 RPC 함수들 ---

        [PunRPC]
        private void EquipItemRPC(string itemDataName)
        {
            // 이미 물고 있는 아이템이 있으면 파괴
            if (equippedItemObject != null)
            {
                Destroy(equippedItemObject);
            }
            // Resources 폴더에서 이름에 해당하는 ItemData를 찾아 로드합니다.
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemDataName);

            if (itemData != null && itemData.itemPrefab != null)
            {
                // 찾은 ItemData를 기반으로 3D 모델을 생성하고 입에 붙입니다.
                equippedItemObject = Instantiate(itemData.itemPrefab, mouthAttachPoint);
                equippedItemObject.transform.localPosition = Vector3.zero;
                equippedItemObject.transform.localRotation = Quaternion.identity;
            }
        }

        [PunRPC]
        private void UnequipItemRPC()
        {
            // 입에 물고 있는 아이템 모델이 있다면
            if (equippedItemObject != null)
            {
                // 파괴하고,
                Destroy(equippedItemObject);
                // 변수를 null로 초기화합니다.
                equippedItemObject = null;
            }
        }

        private void EquipItemFromSlot(int slotIndex)
        {
            if (currentState != PlayerState.Normal) return;

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

            // 모든 클라이언트에게 새 아이템 모델을 생성하라는 RPC를 보냅니다.
            photonView.RPC("EquipItemRPC", RpcTarget.All, newItem.name);
            if (UIManager.instance != null)
            {
                UIManager.instance.inventoryUI.UpdateSelection(slotIndex);
            }
        }


        // 장착한 아이템을 사용하는 함수
        private void UseEquippedItem()
        {
            if (currentState != PlayerState.Normal || equippedItem == null) return;

            // 1. 장착한 아이템의 효과가 '퀘스트 효과'인지 확인합니다.
            if (equippedItem.effects != null && equippedItem.effects.Count > 0 && equippedItem.effects[0] is QuestItemEffect)
            {
                Debug.Log(equippedItem.itemName + "은(는) 사용할 수 없는 아이템입니다.");
                // 퀘스트 아이템은 사용할 수 없으므로, 아무것도 하지 않고 함수를 종료합니다.
                return;
            }
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
            if (equippedItem == null) return;
            Debug.Log("아이템 장착 해제 (맨손 상태)");

            // 1. 모든 클라이언트에게 아이템 모델을 파괴하라는 RPC를 먼저 보냅니다.
            photonView.RPC("UnequipItemRPC", RpcTarget.All);

            // 2. RPC를 보낸 후, 로컬 변수들을 초기화합니다.
            equippedItem = null;
            lastEquippedSlot = -1;
            // equippedItemObject = null; // 이 줄을 여기서 삭제합니다. RPC가 처리할 것입니다.

            // 3. UI 업데이트 로직을 실행합니다.
            if (UIManager.instance != null && UIManager.instance.inventoryUI != null)
            {
                UIManager.instance.inventoryUI.UpdateSelection(-1);
            }
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



        private IEnumerator SpeedBoostCoroutine(float newSpeed, float duration)
        {
            float originalSpeed = runSpeed;
            runSpeed = newSpeed;
            Debug.Log("속도 증가! 현재 속도: " + runSpeed);

            yield return new WaitForSeconds(duration);

            runSpeed = originalSpeed;
            Debug.Log("속도 원래대로 복귀. 현재 속도: " + runSpeed);
        }
        // 이 함수는 '아이템 효과' 스크립트가 호출합니다.
        public void ThrowItem(string prefabName, float force)
        {
            // 1. 아이템을 던지는 '나'의 카메라 방향을 계산합니다.
            Vector3 throwDirection = thirdPersonCameraScript.transform.forward;

            // 2. 이 방향과 힘, 생성 위치 정보를 모든 사람에게 RPC로 전달합니다.
            photonView.RPC("ThrowItemRPC", RpcTarget.All, prefabName, force, throwDirection, mouthAttachPoint.position);
        }

        [PunRPC]
        public void ThrowItemRPC(string projectilePrefabName, float force, Vector3 throwDirection, Vector3 spawnRootPosition)
        {
            // 3. 이 RPC는 모든 클라이언트에서 실행되지만, 생성은 마스터 클라이언트가 딱 한 번만 하도록 합니다.
            if (PhotonNetwork.IsMasterClient)
            {
                Vector3 spawnPosition = spawnRootPosition + throwDirection * 0.5f;

                // 4. [핵심] 생성 시점에 '초기 속도'를 데이터로 함께 넘겨줍니다.
                object[] instantiationData = new object[] { throwDirection * force };

                PhotonNetwork.Instantiate(projectilePrefabName, spawnPosition, Quaternion.LookRotation(throwDirection), 0, instantiationData);
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
                bool success = currentInteractable.Interact(this.gameObject); 
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
        private IEnumerator HoldInteractionCoroutine() { if (interactionSlider == null) yield break; float timer = 0f; while (timer < interactionHoldDuration) { timer += Time.deltaTime; interactionSlider.value = timer / interactionHoldDuration; yield return null; } currentInteractable?.Interact(this.gameObject); interactionCoroutine = null; if (interactionSlider != null) interactionSlider.value = 0; }
        private void HandleRecoveryMash()
        {
            // [수정] 오직 'Stunned' 상태일 때만 연타(Recovery) 입력이 작동하도록 조건을 변경합니다.
            if (currentState != PlayerState.Stunned || !photonView.IsMine) return;

            if (inputActions.Player.Recovery.triggered)
            {
                if (Time.time >= lastMashTime + mashCooldown)
                {
                    lastMashTime = Time.time;
                    // 이제 Stunned 상태만 처리하므로 stunMashAmount만 사용합니다.
                    photonView.RPC("AddRecoveryProgress", RpcTarget.All, stunMashAmount);
                }
            }
        }
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
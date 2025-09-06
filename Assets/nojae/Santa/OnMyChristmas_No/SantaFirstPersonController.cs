using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine.AI;

public class SantaFirstPersonController : MonoBehaviourPunCallbacks
{
    //ES
    [SerializeField] private PunchHitbox punchHitbox;

    // 이동 관련
    public float walkSpeed = 5f;
    public float runMultiplier = 1.5f;
    public float mouseSensitivity = 2f;

    // 스태미너 관련
    public float maxStamina = 100f;
    public float staminaDecreaseRate = 20f;
    public float staminaRecoverRate = 10f;
    public float staminaRecoveryDelay = 0.5f;
    public Slider staminaBar;

    // 카메라
    public Transform cameraTransform;
    public float maxLookUp = 80f;
    public float maxLookDown = -80f;

    // 사다리 관련
    public float climbSpeed = 3f;
    private bool isClimbing = false;
    private Transform currentLadder;
    private bool isNearLadder = false;

    // 점프 관련
    public float jumpForce = 5f;
    public Transform groundCheck;
    public float groundCheckDistance = 0.3f;
    // playerLayer를 사용하여 캐릭터 자신의 레이어를 감지 대상에서 제외
    public LayerMask playerLayer;

    // 펀치 관련
    private bool isPunching = false;
    private int punchLayerIndex;
    public float punchRange = 2.0f;

    // 선물 보따리 및 순록 상호작용 관련 변수
    public GameObject giftBagPrefab;
    public Transform bagSpawnPoint;
    public GameObject reindeerPrefab;
    public float interactionDistance = 5f;
    public string reindeerTag = "Reindeer";
    public string sleighTag = "Sleigh";
    private Animator giftBagAnimator;
    private GameObject currentGiftBag;
    private GameObject currentReindeer;
    private bool hasGiftBag = false;
    private bool isNearSleigh = false;
    private MonoBehaviour reindeerControllerScript;

    // 지도 및 순간이동 관련 변수
    public GameObject mapUI;
    public string chimneyTag = "Chimney";
    private bool isNearChimney = false;

    // 순간이동 위치
    public Transform teleportPointA;
    public Transform teleportPointB;
    public Transform teleportPointC;

    // 내부 상태
    private float currentStamina;
    private bool isRecovering = false;
    private bool isRunning;

    private Rigidbody rb;
    private Animator animator;

    private float xRotation = 0f;

    // 순록 기절 상태 저장
    private Dictionary<int, bool> stunnedReindeerStates;

    // 넉백(Knockback) 관련 변수
    public GameObject knockbackItemPrefab;
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.5f;
    private Vector3 knockbackDirection;
    private float knockbackTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        CharacterController charController = GetComponent<CharacterController>();
        if (charController != null)
        {
            Destroy(charController);
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            Destroy(agent);
            Debug.Log("NavMeshAgent 컴포넌트가 감지되어 제거되었습니다.");
        }

        stunnedReindeerStates = new Dictionary<int, bool>();
        currentStamina = maxStamina;

        if (staminaBar != null)
        {
            staminaBar.maxValue = maxStamina;
            staminaBar.value = currentStamina;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        int upperBodyLayerIndex = animator.GetLayerIndex("Upperbody");
        if (upperBodyLayerIndex >= 0)
        {
            animator.SetLayerWeight(upperBodyLayerIndex, 1f);
        }

        punchLayerIndex = animator.GetLayerIndex("Punch");

        if (mapUI != null)
        {
            mapUI.SetActive(false);
        }

        knockbackTimer = 0f;
        knockbackDirection = Vector3.zero;

        if (photonView.IsMine)
        {
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            if (isClimbing)
            {
                Climb();
            }
            else
            {
                if (knockbackTimer <= 0)
                {
                    Move();
                }
                else
                {
                    rb.AddForce(knockbackDirection * Time.fixedDeltaTime, ForceMode.Force);
                    knockbackTimer -= Time.fixedDeltaTime;
                }
            }
        }
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            Look();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Jump();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (isNearLadder)
                {
                    // 사다리 기능 별도 구현 필요
                }
                else if (isNearSleigh && hasGiftBag)
                {
                    DespawnGiftBagAndSpawnReindeer();
                }
                else if (isNearChimney)
                {
                    ToggleMapUI(true);
                }
                else
                {
                    CheckForReindeerInteraction();
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                Punch();
            }
        }
        UpdateStaminaUI();
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = transform.TransformDirection(h, 0f, v).normalized;
        bool isRunningInput = Input.GetKey(KeyCode.LeftShift) && v > 0f;
        isRunning = isRunningInput && currentStamina > 0f && !isRecovering;
        float speed = isRunning ? walkSpeed * runMultiplier : walkSpeed;

        if (isRunning)
        {
            currentStamina -= staminaDecreaseRate * Time.fixedDeltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            if (currentStamina <= 0f && !isRecovering)
                StartCoroutine(StaminaRecoveryDelay());
        }
        else if (!isRecovering)
        {
            currentStamina += staminaRecoverRate * Time.fixedDeltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        Vector3 targetVelocity = direction * speed;
        Vector3 currentVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        Vector3 force = targetVelocity - currentVelocity;

        rb.AddForce(force, ForceMode.VelocityChange);

        float flatSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
        animator.SetFloat("Speed", flatSpeed);
        animator.SetBool("isRunning", isRunning);
    }

    void Jump()
    {
        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetBool("isJumping", true);
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck != null)
        {
            // 플레이어 레이어만 제외하고 모든 콜라이더를 감지
            return Physics.CheckSphere(groundCheck.position, groundCheckDistance, ~playerLayer);
        }
        return false;
    }

    void CheckForReindeerInteraction()
    {
        if (hasGiftBag)
        {
            return;
        }

        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, interactionDistance))
        {
            if (hit.collider.CompareTag(reindeerTag))
            {
                PhotonView hitPhotonView = hit.collider.GetComponent<PhotonView>();
                if (hitPhotonView != null && stunnedReindeerStates.ContainsKey(hitPhotonView.ViewID) && stunnedReindeerStates[hitPhotonView.ViewID])
                {
                    Debug.Log("기절한 순록을 바라보고 F키를 눌렀습니다. 선물 보따리를 생성하고 순록을 제거합니다.");
                    SpawnGiftBagAndRemoveReindeer(hit.collider.gameObject);
                }
                else
                {
                    Debug.Log("순록이 기절하지 않았습니다. 먼저 펀치로 기절시켜야 합니다.");
                }
            }
        }
    }

    void SpawnGiftBagAndRemoveReindeer(GameObject reindeerObject)
    {
        if (giftBagPrefab == null || bagSpawnPoint == null)
        {
            Debug.LogError("Gift Bag Prefab or Bag Spawn Point is not set!");
            return;
        }

        currentGiftBag = Instantiate(giftBagPrefab, bagSpawnPoint.position, bagSpawnPoint.rotation, bagSpawnPoint);
        giftBagAnimator = currentGiftBag.GetComponent<Animator>();

        if (giftBagAnimator != null)
        {
            giftBagAnimator.SetTrigger("OnSpawn");
            Debug.Log("선물 보따리가 생성되고 애니메이션이 재생됩니다.");
        }
        else
        {
            Debug.LogError("Gift Bag Prefab does not have an Animator component.");
        }

        hasGiftBag = true;
        currentReindeer = reindeerObject;
        reindeerControllerScript = currentReindeer.GetComponent("ReindeerController") as MonoBehaviour;

        PhotonView reindeerPhotonView = currentReindeer.GetComponent<PhotonView>();
        if (reindeerPhotonView != null && stunnedReindeerStates.ContainsKey(reindeerPhotonView.ViewID))
        {
            stunnedReindeerStates.Remove(reindeerPhotonView.ViewID);
        }

        if (reindeerControllerScript != null)
        {
            reindeerControllerScript.enabled = false;
        }
        currentReindeer.SetActive(false);
    }

    void DespawnGiftBagAndSpawnReindeer()
    {
        if (currentGiftBag != null)
        {
            Destroy(currentGiftBag);
            hasGiftBag = false;
            Debug.Log("썰매 근처에서 선물 보따리가 제거되었습니다.");
        }

        if (currentReindeer != null)
        {
            currentReindeer.transform.position = transform.position + transform.forward * 2f;
            currentReindeer.SetActive(true);
            Debug.Log("순록이 다시 나타났습니다.");

            if (reindeerControllerScript != null)
            {
                reindeerControllerScript.enabled = true;
            }
        }
    }

    public void ToggleMapUI(bool show)
    {
        if (mapUI != null)
        {
            mapUI.SetActive(show);

            if (show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void TeleportTo(Transform targetLocation)
    {
        if (targetLocation != null)
        {
            rb.position = targetLocation.position;
            rb.velocity = Vector3.zero;
            Debug.Log($"산타가 {targetLocation.name}으로 순간이동했습니다.");
            ToggleMapUI(false);
        }
    }

    void Climb()
    {
        float v = Input.GetAxis("Vertical");
        Vector3 climbDirection = Vector3.up * v * climbSpeed;
        rb.velocity = new Vector3(rb.velocity.x, climbDirection.y, rb.velocity.z);
        animator.SetFloat("Speed", Mathf.Abs(v));
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
        transform.Rotate(Vector3.up * mouseX);
    }

    void Punch()
    {
        if (!isPunching)
        {
            StartCoroutine(PunchRoutine());

            if (cameraTransform == null)
            {
                Debug.LogError("Camera Transform이 할당되지 않았습니다. 인스펙터 창에서 할당해주세요.");
                return;
            }

            RaycastHit hit;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, punchRange))
            {
                PhotonView hitPhotonView = hit.collider.GetComponent<PhotonView>();

                if (hit.collider.CompareTag(reindeerTag) && hitPhotonView != null)
                {
                    photonView.RPC("ReceivePunch", RpcTarget.All, hitPhotonView.ViewID);
                    stunnedReindeerStates[hitPhotonView.ViewID] = true;
                    Debug.Log($"순록 (ID: {hitPhotonView.ViewID})에게 펀치 신호를 보내고 기절 상태로 설정했습니다.");
                }
            }
        }
    }

    [PunRPC]
    public void ApplyKnockback(Vector3 direction)
    {
        knockbackDirection = direction.normalized * knockbackForce;
        knockbackTimer = knockbackDuration;
        Debug.Log("넉백이 적용되었습니다.");
    }

    IEnumerator PunchRoutine()
    {
        isPunching = true;
        if (punchLayerIndex >= 0)
        {
            animator.SetLayerWeight(punchLayerIndex, 1f);
        }
        animator.SetBool("isPunching_Right", true);
        yield return new WaitForSeconds(0.5f);
        animator.SetBool("isPunching_Right", false);
        if (punchLayerIndex >= 0)
        {
            animator.SetLayerWeight(punchLayerIndex, 0f);
        }
        isPunching = false;
    }

    IEnumerator StaminaRecoveryDelay()
    {
        isRecovering = true;
        yield return new WaitForSeconds(staminaRecoveryDelay);
        isRecovering = false;
    }

    void UpdateStaminaUI()
    {
        if (staminaBar != null)
        {
            staminaBar.value = currentStamina;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // 땅에 닿았을 때 점프 애니메이션 종료
        if (animator.GetBool("isJumping"))
        {
            animator.SetBool("isJumping", false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ladder"))
        {
            isNearLadder = true;
            currentLadder = other.transform;
        }
        if (other.CompareTag(sleighTag))
        {
            isNearSleigh = true;
        }
        if (other.CompareTag(chimneyTag))
        {
            isNearChimney = true;
        }

        if (other.CompareTag("KnockbackItem"))
        {
            Vector3 knockbackDir = transform.position - other.transform.position;
            if (photonView.IsMine)
            {
                photonView.RPC("ApplyKnockback", RpcTarget.All, knockbackDir);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ladder"))
        {
            isNearLadder = false;
            currentLadder = null;
        }
        if (other.CompareTag(sleighTag))
        {
            isNearSleigh = false;
        }
        if (other.CompareTag(chimneyTag))
        {
            isNearChimney = false;
        }
    }
    // ES
    public void AE_PunchOpen() { punchHitbox?.ActivateWindow(); }
    public void AE_PunchClose() { punchHitbox?.DeactivateWindow(); }
}
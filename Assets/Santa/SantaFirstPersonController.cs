using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class SantaFirstPersonController : MonoBehaviour
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
    public float jumpHeight = 1.5f;
    private bool isJumping = false;

    // 펀치 관련
    private bool isPunching = false;
    private int punchLayerIndex;

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
    public GameObject mapUI; // 지도 UI(캔버스) 오브젝트
    public string chimneyTag = "Chimney"; // 굴뚝 태그
    private bool isNearChimney = false; // 굴뚝 근처 여부

    // 순간이동 위치
    public Transform teleportPointA;
    public Transform teleportPointB;

    // 내부 상태
    private float currentStamina;
    private bool isRecovering = false;
    private bool isRunning;

    private CharacterController controller;
    private Animator animator;

    private float verticalVelocity = 0f;
    private float gravity = -9.81f;
    private float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

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
    }

    void Update()
    {
        if (isClimbing)
        {
            Climb();
        }
        else
        {
            Move();
            Look();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isNearLadder)
            {
                ToggleLadder();
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

        UpdateStaminaUI();
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
                Debug.Log("순록을 바라보고 F키를 눌렀습니다. 선물 보따리를 생성하고 순록을 제거합니다.");
                SpawnGiftBagAndRemoveReindeer(hit.collider.gameObject);
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
            // 선물 보따리 애니메이터의 "OnSpawn" 트리거를 설정하여 애니메이션을 재생합니다.
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
                Time.timeScale = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void TeleportTo(Transform targetLocation)
    {
        if (targetLocation != null)
        {
            controller.enabled = false;
            transform.position = targetLocation.position;
            controller.enabled = true;
            Debug.Log($"산타가 {targetLocation.name}으로 순간이동했습니다.");

            ToggleMapUI(false);
        }
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        Vector3 direction = transform.TransformDirection(input.normalized);
        bool isRunningInput = Input.GetKey(KeyCode.LeftShift) && v > 0f;
        isRunning = isRunningInput && currentStamina > 0f && !isRecovering;
        float speed = isRunning ? walkSpeed * runMultiplier : walkSpeed;

        if (isRunning)
        {
            currentStamina -= staminaDecreaseRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            if (currentStamina <= 0f && !isRecovering)
                StartCoroutine(StaminaRecoveryDelay());
        }
        else if (!isRecovering)
        {
            currentStamina += staminaRecoverRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isJumping = true;
                animator.SetBool("isJumping", true);
            }
            else if (isJumping)
            {
                isJumping = false;
                animator.SetBool("isJumping", false);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        float flatSpeed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;
        animator.SetFloat("Speed", flatSpeed);
        animator.SetBool("isRunning", isRunning);
    }

    void Climb()
    {
        float v = Input.GetAxis("Vertical");
        Vector3 climbDirection = Vector3.up * v * climbSpeed;
        verticalVelocity = 0f;
        controller.Move(climbDirection * Time.deltaTime);
        animator.SetFloat("Speed", Mathf.Abs(v));
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Punch()
    {
        if (!isPunching)
            StartCoroutine(PunchRoutine());
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

    void ToggleLadder()
    {
        isClimbing = !isClimbing;
        if (isClimbing)
        {
            verticalVelocity = 0f;
            animator.SetBool("isClimbing", true);
            if (currentLadder != null)
            {
                Vector3 lookDir = -currentLadder.forward;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }
        else
        {
            animator.SetBool("isClimbing", false);
            verticalVelocity = -2f;
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
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ladder"))
        {
            isNearLadder = false;
            if (isClimbing)
            {
                isClimbing = false;
                animator.SetBool("isClimbing", false);
                verticalVelocity = -2f;
            }
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
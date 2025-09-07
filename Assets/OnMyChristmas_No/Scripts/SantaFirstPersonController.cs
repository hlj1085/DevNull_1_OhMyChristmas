using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine.AI;

public class SantaFirstPersonController : MonoBehaviourPunCallbacks, IPunObservable
{
    // 이동 관련
    public float walkSpeed = 5f;
    public float runMultiplier = 1.5f;
    public float mouseSensitivity = 2f;
    public float turnSpeed = 8f;
    public float moveSmoothTime = 0.1f;

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
    public float jumpCooldown = 3f;
    private float lastJumpTime = -3f;
    private bool _isGrounded;
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
    private Vector2 moveInput;
    private bool isRunningInput;

    private Rigidbody rb;
    private Animator animator;

    private float xRotation = 0f;

    private Dictionary<int, bool> stunnedReindeerStates = new Dictionary<int, bool>();

    // 넉백(Knockback) 관련 변수
    public GameObject knockbackItemPrefab;
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.5f;
    private Vector3 knockbackDirection;
    private float knockbackTimer = 0f;

    // 네트워크 동기화를 위한 변수
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkSpeed;
    private bool networkIsJumping;
    private bool networkIsRunning;

    private Vector3 currentHorizontalVelocity;
    private Vector3 smoothDampVelocity;

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

        currentStamina = maxStamina;

        int upperBodyLayerIndex = animator.GetLayerIndex("Upperbody");
        if (upperBodyLayerIndex >= 0)
        {
            animator.SetLayerWeight(upperBodyLayerIndex, 1f);
        }

        punchLayerIndex = animator.GetLayerIndex("Punch");

        knockbackTimer = 0f;
        knockbackDirection = Vector3.zero;

        if (!photonView.IsMine)
        {
            if (cameraTransform != null)
            {
                cameraTransform.gameObject.SetActive(false);
            }
            if (staminaBar != null)
            {
                staminaBar.gameObject.SetActive(false);
            }
            return;
        }

        if (staminaBar != null)
        {
            staminaBar.maxValue = maxStamina;
            staminaBar.value = currentStamina;
        }

        if (mapUI != null)
        {
            mapUI.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.identity;
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            _isGrounded = false;

            if (isClimbing)
            {
                Climb();
            }
            else
            {
                if (knockbackTimer <= 0)
                {
                    ApplyMovement();
                }
                else
                {
                    rb.AddForce(knockbackDirection * Time.fixedDeltaTime, ForceMode.Force);
                    knockbackTimer -= Time.fixedDeltaTime;
                }
            }
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.fixedDeltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.fixedDeltaTime * 10f);
        }
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            Look();
            UpdateStaminaUI();
        }
        else
        {
            animator.SetFloat("Speed", networkSpeed);
            animator.SetBool("isJumping", networkIsJumping);
            animator.SetBool("isRunning", networkIsRunning);
        }
    }

    // Player Input 시스템의 "Jump" 액션이 트리거될 때 호출됩니다.
    public void OnJump(InputAction.CallbackContext context)
    {
        if (photonView.IsMine && context.performed)
        {
            if (Time.time >= lastJumpTime + jumpCooldown)
            {
                photonView.RPC("JumpRPC", RpcTarget.All);
                lastJumpTime = Time.time;
            }
        }
    }

    // Player Input 시스템의 "Move" 액션이 트리거될 때 호출됩니다.
    public void OnMove(InputAction.CallbackContext context)
    {
        if (photonView.IsMine)
        {
            moveInput = context.ReadValue<Vector2>();
        }
    }

    // Player Input 시스템의 "Run" 액션이 트리거될 때 호출됩니다.
    public void OnRun(InputAction.CallbackContext context)
    {
        if (photonView.IsMine)
        {
            isRunningInput = context.performed;
        }
    }

    // Player Input 시스템의 "Punch" 액션이 트리거될 때 호출됩니다.
    public void OnPunch(InputAction.CallbackContext context)
    {
        if (photonView.IsMine && context.performed)
        {
            Punch();
        }
    }

    // Player Input 시스템의 "Interact" 액션이 트리거될 때 호출됩니다.
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (photonView.IsMine && context.performed)
        {
            if (isNearLadder)
            {
                // 사다리 로직
            }
            else if (isNearSleigh && hasGiftBag)
            {
                photonView.RPC("DespawnGiftBagAndSpawnReindeerRPC", RpcTarget.All, currentGiftBag.GetPhotonView().ViewID, currentReindeer.GetPhotonView().ViewID);
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
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(rb.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(animator.GetFloat("Speed"));
            stream.SendNext(animator.GetBool("isJumping"));
            stream.SendNext(animator.GetBool("isRunning"));
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkSpeed = (float)stream.ReceiveNext();
            networkIsJumping = (bool)stream.ReceiveNext();
            networkIsRunning = (bool)stream.ReceiveNext();
        }
    }

    private void ApplyMovement()
    {
        if (moveInput.magnitude < 0.1f)
        {
            currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, Vector3.zero, ref smoothDampVelocity, moveSmoothTime);
            rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);
            animator.SetFloat("Speed", 0f);
            animator.SetBool("isRunning", false);
            return;
        }

        float targetSpeed = isRunningInput ? walkSpeed * runMultiplier : walkSpeed;
        isRunning = isRunningInput && moveInput.y > 0f && currentStamina > 0f && !isRecovering;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 desiredMoveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;

        if (desiredMoveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), turnSpeed * Time.fixedDeltaTime);
        }

        Vector3 targetHorizontalVelocity = desiredMoveDirection * targetSpeed;
        currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, targetHorizontalVelocity, ref smoothDampVelocity, moveSmoothTime);

        rb.velocity = new Vector3(currentHorizontalVelocity.x, rb.velocity.y, currentHorizontalVelocity.z);

        float flatSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
        animator.SetFloat("Speed", flatSpeed);
        animator.SetBool("isRunning", isRunning);
    }

    [PunRPC]
    void JumpRPC()
    {
        if (_isGrounded)
        {
            if (photonView.IsMine)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
            animator.SetBool("isJumping", true);
        }
    }

    private bool IsGrounded()
    {
        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (photonView.IsMine)
        {
            if ((playerLayer.value & (1 << collision.gameObject.layer)) == 0)
            {
                _isGrounded = true;
                if (animator.GetBool("isJumping"))
                {
                    animator.SetBool("isJumping", false);
                }
            }
        }
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
            PhotonView hitPhotonView = hit.collider.GetComponent<PhotonView>();
            if (hit.collider.CompareTag(reindeerTag) && hitPhotonView != null)
            {
                if (stunnedReindeerStates.ContainsKey(hitPhotonView.ViewID) && stunnedReindeerStates[hitPhotonView.ViewID])
                {
                    photonView.RPC("SpawnGiftBagAndRemoveReindeerRPC", RpcTarget.MasterClient, hitPhotonView.ViewID);
                    currentReindeer = hit.collider.gameObject;
                    Debug.Log("기절한 순록을 바라보고 F키를 눌렀습니다. 마스터 클라이언트에 선물 보따리 생성 및 순록 제거를 요청합니다.");
                }
                else
                {
                    Debug.Log("순록이 기절하지 않았습니다. 먼저 펀치로 기절시켜야 합니다.");
                }
            }
        }
    }

    [PunRPC]
    void SpawnGiftBagAndRemoveReindeerRPC(int reindeerViewID)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView reindeerPhotonView = PhotonView.Find(reindeerViewID);
            if (reindeerPhotonView != null)
            {
                GameObject newGiftBag = PhotonNetwork.Instantiate(giftBagPrefab.name, bagSpawnPoint.position, bagSpawnPoint.rotation);
                PhotonView newBagView = newGiftBag.GetPhotonView();

                PhotonNetwork.Destroy(reindeerPhotonView.gameObject);

                photonView.RPC("SetGiftBagStateRPC", RpcTarget.All, newBagView.ViewID, true, reindeerViewID);
            }
        }
    }

    [PunRPC]
    void DespawnGiftBagAndSpawnReindeerRPC(int giftBagViewID, int reindeerViewID)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView giftBagView = PhotonView.Find(giftBagViewID);
            if (giftBagView != null)
            {
                PhotonNetwork.Destroy(giftBagView.gameObject);
            }

            GameObject newReindeer = PhotonNetwork.Instantiate(reindeerPrefab.name, transform.position + transform.forward * 2f, Quaternion.identity);
        }

        hasGiftBag = false;
        if (currentGiftBag != null)
        {
            Destroy(currentGiftBag);
        }

        currentReindeer = null;
    }

    [PunRPC]
    public void SetGiftBagStateRPC(int bagViewID, bool hasBag, int reindeerID)
    {
        this.hasGiftBag = hasBag;
        if (hasBag)
        {
            PhotonView bagView = PhotonView.Find(bagViewID);
            if (bagView != null)
            {
                currentGiftBag = bagView.gameObject;
                currentGiftBag.transform.SetParent(bagSpawnPoint);
                currentGiftBag.transform.localPosition = Vector3.zero;
                currentGiftBag.transform.localRotation = Quaternion.identity;

                giftBagAnimator = currentGiftBag.GetComponent<Animator>();
                if (giftBagAnimator != null)
                {
                    giftBagAnimator.SetTrigger("OnSpawn");
                }
                Debug.Log("선물 보따리가 네트워크에 생성되었습니다.");

                if (stunnedReindeerStates.ContainsKey(reindeerID))
                {
                    stunnedReindeerStates.Remove(reindeerID);
                }
            }
        }
        else
        {
            currentGiftBag = null;
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
        if (photonView.IsMine && targetLocation != null)
        {
            photonView.RPC("TeleportToRPC", RpcTarget.All, targetLocation.position);
            ToggleMapUI(false);
        }
    }

    [PunRPC]
    void TeleportToRPC(Vector3 targetPosition)
    {
        if (photonView.IsMine)
        {
            rb.position = targetPosition;
            rb.velocity = Vector3.zero;
            Debug.Log($"산타가 {targetPosition}으로 순간이동했습니다.");
        }
    }

    void Climb()
    {
        float v = moveInput.y;
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
            photonView.RPC("StartPunchAnimationRPC", RpcTarget.All);

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
                    hitPhotonView.RPC("GetStunned", RpcTarget.All);
                    Debug.Log($"순록 (ID: {hitPhotonView.ViewID})에게 펀치 신호를 보냈습니다.");
                }
            }
        }
    }

    [PunRPC]
    void StartPunchAnimationRPC()
    {
        StartCoroutine(PunchRoutine());
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



    void OnTriggerEnter(Collider other)
    {
        if (photonView.IsMine)
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
                photonView.RPC("ApplyKnockback", RpcTarget.All, knockbackDir);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (photonView.IsMine)
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
    }
}
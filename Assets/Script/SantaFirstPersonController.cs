using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class SantaFirstPersonController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runMultiplier = 1.5f;
    public float mouseSensitivity = 2f;

    public float maxStamina = 100f;
    public float staminaDecreaseRate = 20f;
    public float staminaRecoverRate = 10f;
    public float staminaRecoveryDelay = 0.5f;
    public Slider staminaBar;

    public Transform cameraTransform;
    public float maxLookUp = 80f;
    public float maxLookDown = -80f;

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
    }

    void Update()
    {
        Move();
        Look();
        UpdateStaminaUI();
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

        // 스태미너 처리
        if (isRunning)
        {
            currentStamina -= staminaDecreaseRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

            // 스태미너가 0에 도달한 경우 회복 지연 시작
            if (currentStamina <= 0f && !isRecovering)
            {
                StartCoroutine(StaminaRecoveryDelay());
            }
        }
        else if (!isRecovering)
        {
            currentStamina += staminaRecoverRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        // 중력 처리
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 애니메이션 파라미터
        float flatSpeed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;
        if (flatSpeed < 0.05f) flatSpeed = 0f;

        animator.SetFloat("Speed", flatSpeed);
        animator.SetBool("isRunning", isRunning);
    }

    IEnumerator StaminaRecoveryDelay()
    {
        isRecovering = true;
        yield return new WaitForSeconds(staminaRecoveryDelay);
        isRecovering = false;
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

    void UpdateStaminaUI()
    {
        if (staminaBar != null)
        {
            staminaBar.value = currentStamina;
        }
    }
}

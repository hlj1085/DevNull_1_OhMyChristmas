using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("참조")]
    public Transform target;
    public ReindeerController playerController;

    [Header("카메라 설정")]
    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float sensitivity = 0.1f;
    public float pitchMin = -45f;
    public float pitchMax = 60f;

    [Header("카메라 부가 효과")]
    [Range(0.01f, 0.2f)]
    public float rotationSmoothTime = 0.05f;

    [Header("카메라 충돌")]
    public float minDistance = 1f;
    public LayerMask collisionMask;

    [Header("카메라 흔들림")]
    public float shakeIntensity = 0.05f;
    public float shakeSpeed = 15f;

    // --- 비공개 변수 ---
    private Reindeer_Input inputActions;
    private Vector2 lookInput;

    private float targetYaw;
    private float targetPitch;
    private float smoothYaw;
    private float smoothPitch;
    private float yawVelocity, pitchVelocity;

    private float desiredDistance;

    void Awake()
    {
        inputActions = new Reindeer_Input();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        desiredDistance = offset.magnitude;

        smoothYaw = targetYaw = transform.eulerAngles.y;
        smoothPitch = targetPitch = transform.eulerAngles.x;
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null || playerController == null) return;

        targetYaw += lookInput.x * sensitivity;
        targetPitch -= lookInput.y * sensitivity;
        targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);

        smoothYaw = Mathf.SmoothDamp(smoothYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        smoothPitch = Mathf.SmoothDamp(smoothPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);

        Quaternion rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0);

        Vector3 desiredPosition = target.position + rotation * offset;
        Vector3 raycastOrigin = target.position + Vector3.up * 1.0f;
        Vector3 directionToCamera = (desiredPosition - raycastOrigin).normalized;
        float distanceToCamera = Vector3.Distance(raycastOrigin, desiredPosition);

        Debug.DrawRay(raycastOrigin, directionToCamera * distanceToCamera, Color.red);

        if (Physics.Raycast(raycastOrigin, directionToCamera, out RaycastHit hit, distanceToCamera, collisionMask))
        {
            transform.position = raycastOrigin + directionToCamera * (hit.distance - 0.2f);
        }
        else
        {
            transform.position = desiredPosition;
        }

        ApplyShake();
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    private void ApplyShake()
    {
        if (playerController.IsMoving)
        {
            float shakeTimer = Time.time * shakeSpeed;
            Vector3 shakeOffset = (transform.up * Mathf.Sin(shakeTimer) + transform.right * Mathf.Cos(shakeTimer * 1.3f)) * shakeIntensity;
            transform.position += shakeOffset;
        }
    }
}
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

    // [추가된 변수] 이 값이 클수록 카메라 회전이 더 부드러워집니다.
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

    // [수정된 변수] 목표 회전 값과 현재 회전 값을 분리합니다.
    private float targetYaw;
    private float targetPitch;
    private float smoothYaw;
    private float smoothPitch;
    private float yawVelocity, pitchVelocity; // SmoothDamp를 위한 변수

    private float desiredDistance;

    void Awake()
    {
        inputActions = new Reindeer_Input();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        desiredDistance = offset.magnitude;

        // [추가] 시작 시 현재 카메라의 각도를 초기값으로 설정
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

        // 마우스 입력에 따라 '목표' 회전 값만 변경합니다.
        targetYaw += lookInput.x * sensitivity;
        targetPitch -= lookInput.y * sensitivity;
        targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);

        // [수정된 로직] SmoothDamp를 사용하여 현재 각도를 목표 각도로 부드럽게 이동시킵니다.
        smoothYaw = Mathf.SmoothDamp(smoothYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        smoothPitch = Mathf.SmoothDamp(smoothPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);

        // 최종적으로 부드러워진 각도를 사용하여 회전을 계산합니다.
        Quaternion rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0);

        // --- 카메라 충돌 및 위치 적용 로직 (이전과 동일) ---
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
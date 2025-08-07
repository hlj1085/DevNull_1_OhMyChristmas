using UnityEngine;
using UnityEngine.InputSystem; // Input System 사용

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("참조")]
    public Transform target;
    public ReindeerController playerController; // 플레이어 컨트롤러 참조 추가

    [Header("카메라 설정")]
    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float sensitivity = 0.1f; // Input System 사용 시 감도 조절 필요
    public float pitchMin = -45f;
    public float pitchMax = 60f;

    [Header("카메라 충돌")]
    public float minDistance = 1f;
    public LayerMask collisionMask;

    [Header("카메라 흔들림")]
    public float shakeIntensity = 0.05f;
    public float shakeSpeed = 15f;

    // 비공개 변수
    private Reindeer_Input inputActions; // Input Actions 참조
    private Vector2 lookInput;
    private float yaw;
    private float pitch = 10f;
    private Vector3 currentVelocity;
    private float shakeTimer = 0f;
    private float desiredDistance;

    void Awake()
    {
        // 입력 시스템 초기화
        inputActions = new Reindeer_Input();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        desiredDistance = offset.magnitude;
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

        // 마우스 입력 처리 (새로운 Input System)
        yaw += lookInput.x * sensitivity;
        pitch -= lookInput.y * sensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        // 카메라 충돌 처리
        Vector3 desiredOffset = rotation * offset.normalized * desiredDistance;
        Vector3 raycastOrigin = target.position + Vector3.up * 1.0f; // 레이캐스트 시작점 조정
        if (Physics.Raycast(raycastOrigin, desiredOffset.normalized, out RaycastHit hit, desiredDistance, collisionMask))
        {
            desiredOffset = desiredOffset.normalized * Mathf.Max(hit.distance - 0.2f, minDistance);
        }

        Vector3 desiredPosition = target.position + desiredOffset;

        // 흔들림 (플레이어 컨트롤러의 상태를 직접 참조)
        Vector3 shakeOffset = Vector3.zero;
        if (playerController.IsMoving) // 플레이어의 이동 상태를 가져옴
        {
            shakeTimer += Time.deltaTime * shakeSpeed;
            shakeOffset = (transform.up * Mathf.Sin(shakeTimer) + transform.right * Mathf.Cos(shakeTimer * 1.3f)) * shakeIntensity;
        }

        // 위치 적용
        transform.position = desiredPosition + shakeOffset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
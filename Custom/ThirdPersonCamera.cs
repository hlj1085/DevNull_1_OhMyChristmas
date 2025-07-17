using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float sensitivity = 3f;
    public float pitchMin = -45f;
    public float pitchMax = 60f;
    public float smoothTime = 0.07f;

    private float yaw;
    private float pitch = 10f;
    private Vector3 currentVelocity;

    [Header("카메라 충돌")]
    public float minDistance = 1f;
    public float maxDistance = 4f;
    public LayerMask collisionMask;

    [Header("카메라 흔들림")]
    public float shakeIntensity = 0.05f;
    public float shakeSpeed = 15f;
    private float shakeTimer = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        // 카메라 충돌 감지
        if (Physics.Raycast(target.position, (desiredPosition - target.position).normalized, out RaycastHit hit, maxDistance, collisionMask))
        {
            desiredPosition = target.position + (desiredPosition - target.position).normalized * Mathf.Clamp(hit.distance - 0.2f, minDistance, maxDistance);
        }

        // 흔들림 (이동 중에만)
        bool isMoving = Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;
        Vector3 shakeOffset = Vector3.zero;
        if (isMoving)
        {
            shakeTimer += Time.deltaTime * shakeSpeed;
            shakeOffset = new Vector3(Mathf.Sin(shakeTimer), Mathf.Cos(shakeTimer * 1.3f), 0f) * shakeIntensity;
        }

        // 위치 적용
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition + shakeOffset, ref currentVelocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}

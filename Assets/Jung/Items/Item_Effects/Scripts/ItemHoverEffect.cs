using UnityEngine;

public class ItemHoverEffect : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("초당 회전 속도입니다.")]
    public float rotationSpeed = 50f;

    [Header("상하 움직임 설정")]
    [Tooltip("위아래로 움직이는 속도입니다.")]
    public float hoverSpeed = 1.5f;
    [Tooltip("위아래로 움직이는 최대 높이입니다.")]
    public float hoverHeight = 0.1f;

    // 아이템이 처음 생성된 위치를 저장할 변수
    private Vector3 startPosition;

    void Start()
    {
        // 이 스크립트가 시작될 때, 아이템의 현재 위치를 기록합니다.
        startPosition = transform.position;
    }

    void Update()
    {
        // 1. 회전: Y축(Vector3.up)을 기준으로 계속 회전시킵니다.
        // Space.World를 사용하여 오브젝트의 로컬 축이 아닌, 월드 축을 기준으로 회전합니다.
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. 상하 움직임 (Bobbing): Sin 함수를 이용해 부드러운 파도 같은 움직임을 만듭니다.
        // Time.time * hoverSpeed 값에 따라 -1과 1 사이를 계속 왕복합니다.
        float yOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;

        // 처음 위치를 기준으로 Y값만 계속 변경하여 위아래로 움직이는 효과를 줍니다.
        transform.position = new Vector3(startPosition.x, startPosition.y + yOffset, startPosition.z);
    }
}
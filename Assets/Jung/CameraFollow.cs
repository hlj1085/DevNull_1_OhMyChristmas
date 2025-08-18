using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // 카메라가 따라다닐 대상
    public float smoothSpeed = 0.125f; // 카메라 이동의 부드러움 정도
    public Vector3 offset; // 카메라와 대상 사이의 거리

    // LateUpdate는 캐릭터의 움직임이 모두 끝난 후에 호출되므로 카메라 추적에 적합합니다.
    void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치 = 대상의 위치 + 설정된 거리(offset)
        Vector3 desiredPosition = target.position + offset;
        // 현재 위치에서 목표 위치로 부드럽게 이동
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // 항상 대상을 바라보도록 설정
        transform.LookAt(target);
    }

    /// <summary>
    /// 카메라가 따라다닐 대상을 외부에서 변경하는 함수
    /// </summary>
    /// <param name="newTarget">새로운 대상의 Transform</param>
    public void SetTarget(Transform newTarget)
    {
        Debug.Log("카메라 타겟 변경: " + newTarget.name);
        target = newTarget;
    }
}
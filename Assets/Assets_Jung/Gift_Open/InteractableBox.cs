using UnityEngine;
using UnityEngine.UI;

public class InteractableBox : MonoBehaviour
{
    [Tooltip("상자가 열리는 애니메이션을 제어할 Animator")]
    public Animator boxAnimator;

    [Tooltip("상호작용 시 채워질 UI 슬라이더")]
    public Slider interactionSlider;

    [Header("상자 파츠")]
    [Tooltip("상자 뚜껑 오브젝트")]
    public GameObject boxHead;
    [Tooltip("열린 상자 베이스 오브젝트")]
    public GameObject boxBase;

    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;

        if (interactionSlider != null)
        {
            interactionSlider.gameObject.SetActive(false);
        }

        // 초기 상태 설정
        if (boxHead != null) boxHead.SetActive(true);
        if (boxBase != null) boxBase.SetActive(true);
    }

    void Update()
    {
        if (interactionSlider != null && interactionSlider.gameObject.activeSelf)
        {
            if (cameraTransform != null)
            {
                interactionSlider.transform.LookAt(interactionSlider.transform.position + cameraTransform.forward);
            }
        }
    }

    // 상호작용 성공 시 호출되는 함수
    public void OpenBox()
    {
        if (boxAnimator != null)
        {
            // Animator의 "Open" 트리거를 호출하여 애니메이션 실행
            boxAnimator.SetTrigger("Open");
        }

        // 뚜껑은 비활성화하고, 베이스는 활성화
        if (boxHead != null) boxHead.SetActive(false);
        if (boxBase != null) boxBase.SetActive(true);

        // 슬라이더 UI 비활성화
        if (interactionSlider != null)
        {
            interactionSlider.gameObject.SetActive(false);
        }
    }
}
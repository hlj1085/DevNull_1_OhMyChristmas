using UnityEngine;
using TMPro; // TextMeshPro UI를 사용하기 위해 추가

public class FairyController : MonoBehaviour
{
    // GameManager와 ReindeerController를 가져올 변수
    private GameManager gameManager;
    private ReindeerController reindeerController;

    // UI 텍스트 오브젝트
    public TextMeshProUGUI interactionText;

    void Start()
    {
        // GameManager 오브젝트를 찾아 스크립트를 가져옵니다.
        gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("GameManager 스크립트를 찾을 수 없습니다. Hierarchy에 추가했는지 확인해주세요.");
        }

        // UI 텍스트를 숨깁니다.
        if (interactionText != null)
        {
            interactionText.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 순록이 요정의 트리거 영역에 들어오면
        if (other.CompareTag("Reindeer"))
        {
            // ReindeerController 스크립트 가져오기
            reindeerController = other.GetComponent<ReindeerController>();

            // 상호작용 UI를 표시합니다.
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // 순록이 트리거 안에 있고 F키를 누르면
        if (other.CompareTag("Reindeer") && Input.GetKeyDown(KeyCode.F))
        {
            // 순록이 요정가루를 가지고 있는지 확인
            //if (reindeerController != null && reindeerController.HasDust())
            {
                // GameManager에게 요정가루를 전달합니다.
                //gameManager.ReceiveDust();

                // 순록이 가진 요정가루 1개를 사용합니다.
                //reindeerController.UseDust();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 순록이 트리거 영역을 벗어나면
        if (other.CompareTag("Reindeer"))
        {
            // 상호작용 UI를 숨깁니다.
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(false);
            }
            reindeerController = null;
        }
    }
}
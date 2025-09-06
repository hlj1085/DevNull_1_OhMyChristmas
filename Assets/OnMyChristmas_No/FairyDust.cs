using UnityEngine;

public class FairyDust : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 순록이 요정가루에 닿으면
        if (other.CompareTag("Reindeer"))
        {
            // 순록의 ReindeerController 스크립트를 가져와 요정가루를 수집하라고 알립니다.
            ReindeerController reindeer = other.GetComponent<ReindeerController>();
            if (reindeer != null)
            {
               // reindeer.CollectDust();
            }
            
            // 요정가루 오브젝트를 비활성화하여 사라지게 만듭니다.
            gameObject.SetActive(false);
        }
    }
}
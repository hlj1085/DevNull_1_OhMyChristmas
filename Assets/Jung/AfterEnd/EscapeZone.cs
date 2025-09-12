using UnityEngine;
using Photon.Pun;

public class EscapeZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 트리거에 닿은 오브젝트가 순록인지 태그로 확인합니다.
        if (other.CompareTag("Reindeer"))
        {
            // ReindeerController 컴포넌트를 가져옵니다.
            ReindeerController reindeer = other.GetComponent<ReindeerController>();

            // 순록이 맞고, 로컬 플레이어(자신)일 경우에만 엔딩 로직을 호출합니다.
            // (모든 플레이어가 각자 호출하는 것을 방지)
            if (reindeer != null && reindeer.GetComponent<PhotonView>().IsMine)
            {
                Debug.Log("탈출 성공! 게임 종료를 모든 플레이어에게 알립니다.");
                // FairyManager에게 게임을 종료하라고 알립니다.
                FairyManager.instance.ReindeerEscaped();
            }
        }
    }
}
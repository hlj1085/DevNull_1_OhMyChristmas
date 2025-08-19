// SantaCapture.cs (예시)
using UnityEngine;
using Photon.Pun;

public class SantaCapture : MonoBehaviour
{
    [Tooltip("산타가 들고 다니는 보따리 오브젝트")]
    public GameObject sackObject;
    private PhotonView sackPhotonView;

    void Start()
    {
        // 보따리가 PhotonView를 가지고 있는지 확인하고 저장
        sackPhotonView = sackObject.GetComponent<PhotonView>();
        if (sackPhotonView == null)
        {
            Debug.LogError("보따리에 PhotonView 컴포넌트가 없습니다!");
        }
    }

    // 예시: 순록과 부딪혔을 때 포획 시도
    private void OnTriggerEnter(Collider other)
    {
        // 순록 컨트롤러를 찾음
        ReindeerController reindeer = other.GetComponent<ReindeerController>();

        // 순록이 있고, 기절 상태일 때만 포획
        if (reindeer != null && reindeer.CurrentState == ReindeerController.PlayerState.Stunned)
        {
            Debug.Log(reindeer.name + " 포획 시도!");
            PhotonView reindeerPhotonView = reindeer.GetComponent<PhotonView>();

            if (reindeerPhotonView != null && sackPhotonView != null)
            {
                // 순록에게 GetCaptured RPC를 호출하면서, 내 보따리의 ID를 넘겨줌
                reindeerPhotonView.RPC("GetCaptured", RpcTarget.All, sackPhotonView.ViewID);
            }
        }
    }
}
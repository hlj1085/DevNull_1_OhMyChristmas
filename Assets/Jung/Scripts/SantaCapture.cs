using UnityEngine;
using Photon.Pun;

public class SantaCapture : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("산타가 들고 다니는 보따리 오브젝트")]
    public GameObject sackObject;
    [Tooltip("순록을 묶을 썰매 오브젝트")]
    public GameObject sleighObject; // 썰매 참조 추가

    // --- 비공개 변수 ---
    private ReindeerController capturedReindeer; // 현재 잡고 있는 순록을 저장할 변수
    private PhotonView sackPhotonView;
    private PhotonView sleighPhotonView;

    void Start()
    {
        sackPhotonView = sackObject.GetComponent<PhotonView>();
        if (sackPhotonView == null)
        {
            Debug.LogError("보따리에 PhotonView 컴포넌트가 없습니다!");
        }

        // 썰매의 PhotonView도 미리 찾아둡니다.
        if (sleighObject != null)
        {
            sleighPhotonView = sleighObject.GetComponent<PhotonView>();
            if (sleighPhotonView == null)
            {
                Debug.LogError("썰매에 PhotonView 컴포넌트가 없습니다!");
            }
        }
    }

    // --- [추가] --- 테스트를 위한 Update 함수
    void Update()
    {
        // '6'번 키를 누르고, 현재 잡고 있는 순록이 있을 때
        if (Input.GetKeyDown(KeyCode.Alpha6) && capturedReindeer != null && sleighPhotonView != null)
        {
            Debug.Log(capturedReindeer.name + "을(를) 썰매에 묶기 시도!");

            // 잡고 있는 순록에게 "이 썰매의 0번 자리에 묶여라"고 직접 명령
            PhotonView reindeerView = capturedReindeer.GetComponent<PhotonView>();
            if (reindeerView != null)
            {
                // ReindeerController의 AttachToSleigh 함수를 호출
                reindeerView.RPC("AttachToSleigh", RpcTarget.All, sleighPhotonView.ViewID, 0); // 0번 자리에 묶기

                // 썰매에 묶었으므로, 더 이상 잡고 있는 상태가 아님
                capturedReindeer = null;
            }
        }
    }

    // 순록과 부딪혔을 때 포획 시도
    private void OnTriggerEnter(Collider other)
    {
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

                // --- [추가] --- 어떤 순록을 잡았는지 기록
                capturedReindeer = reindeer;
            }
        }
    }
}
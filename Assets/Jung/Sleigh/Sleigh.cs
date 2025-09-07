using UnityEngine;
using Photon.Pun;

public class Sleigh : MonoBehaviour
{
    [Tooltip("순록들이 묶일 4개의 위치(Transform)")]
    public Transform[] attachmentPoints = new Transform[4];
    private PhotonView photonView;
    private int[] attachedReindeerIDs = new int[4]; // 각 자리에 묶인 순록의 ID

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    // 산타가 호출할 함수: "이 순록을 빈 자리에 묶어줘!"
    public void AttachReindeer(ReindeerController reindeer)
    {
        // 마스터 클라이언트만 자리 배정 가능
        if (!PhotonNetwork.IsMasterClient || reindeer == null) return;

        // 1. 빈 자리를 찾음
        int emptySlotIndex = -1;
        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            if (attachedReindeerIDs[i] == 0)
            {
                emptySlotIndex = i;
                break;
            }
        }

        if (emptySlotIndex == -1)
        {
            Debug.Log("썰매에 빈 자리가 없습니다.");
            return;
        }

        // 2. 모든 클라이언트에게 "이 순록을 이 자리에 묶었다"고 알림 (RPC)
        PhotonView reindeerPhotonView = reindeer.GetComponent<PhotonView>();
        if (reindeerPhotonView != null)
        {
            photonView.RPC("SyncAttachment", RpcTarget.AllBuffered, reindeerPhotonView.ViewID, emptySlotIndex);
        }
    }

    [PunRPC]
    private void SyncAttachment(int reindeerViewID, int slotIndex)
    {
        // 모든 클라이언트가 똑같이 자리 정보를 기록
        attachedReindeerIDs[slotIndex] = reindeerViewID;

        // 해당 순록에게 "이 썰매의 이 자리에 붙어라"고 최종 명령
        PhotonView reindeerPhotonView = PhotonView.Find(reindeerViewID);
        if (reindeerPhotonView != null)
        {
            reindeerPhotonView.RPC("AttachToSleigh", RpcTarget.All, photonView.ViewID, slotIndex);
        }
    }
}
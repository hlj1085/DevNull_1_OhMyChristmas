using UnityEngine;
using Photon.Pun;

public class Sleigh : MonoBehaviour
{
    [Tooltip("순록들이 묶일 4개의 위치(Transform)를 순서대로 연결하세요.")]
    public Transform[] attachmentPoints = new Transform[4];

    [Tooltip("썰매가 주변의 잡힌 순록을 찾을 수 있는 최대 반경입니다.")]
    public float searchRadius = 15f;

    private PhotonView photonView;

    // 각 자리에 어떤 순록이 묶여있는지 ID로 기록 (0 = 비어있음)
    private int[] attachedReindeerIDs = new int[4];

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    // 이 함수는 산타가 썰매와 상호작용했을 때 호출됩니다.
    public void OnSantaInteraction()
    {
        // 마스터 클라이언트(방장)만 순록을 묶는 결정을 내리도록 하여 충돌을 방지합니다.
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("마스터 클라이언트만 순록을 썰매에 묶을 수 있습니다.");
            return;
        }

        // 1. 비어있는 썰매 자리를 찾습니다.
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

        // 2. 썰매 주변에 '포획된' 상태의 순록이 있는지 찾습니다.
        Collider[] colliders = Physics.OverlapSphere(transform.position, searchRadius);
        foreach (var col in colliders)
        {
            ReindeerController reindeer = col.GetComponent<ReindeerController>();

            // 순록이 있고, '포획된' 상태이며, 아직 썰매에 묶이지 않았다면
            if (reindeer != null && reindeer.CurrentState == ReindeerController.PlayerState.Captured && !reindeer.IsAttachedToSleigh)
            {
                // 3. 찾은 순록에게 "n번 자리에 묶여라" 라고 모든 클라이언트에게 RPC 명령을 보냅니다.
                Debug.Log($"{reindeer.name}을(를) {emptySlotIndex}번 자리에 묶도록 명령합니다.");

                PhotonView reindeerPhotonView = reindeer.GetComponent<PhotonView>();

                // 모든 클라이언트에게 AttachReindeerRPC 함수를 실행하라고 알림
                photonView.RPC("AttachReindeerRPC", RpcTarget.All, reindeerPhotonView.ViewID, emptySlotIndex);

                // 한 명만 묶고 종료
                return;
            }
        }

        Debug.Log("주변에 묶을 수 있는 순록이 없습니다.");
    }

    // 이 RPC는 Sleigh 스크립트가 모든 클라이언트에게 실행하라고 보내는 신호입니다.
    [PunRPC]
    private void AttachReindeerRPC(int reindeerViewID, int slotIndex)
    {
        PhotonView reindeerPhotonView = PhotonView.Find(reindeerViewID);
        if (reindeerPhotonView != null)
        {
            // 1. 어떤 순록이 몇 번 자리에 묶였는지 기록합니다.
            attachedReindeerIDs[slotIndex] = reindeerViewID;

            // 2. 해당 순록에게 진짜로 썰매에 붙으라는 명령을 내립니다.
            reindeerPhotonView.RPC("AttachToSleigh", RpcTarget.All, photonView.ViewID, slotIndex);
        }
    }
}
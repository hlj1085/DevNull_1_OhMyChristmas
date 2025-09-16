using UnityEngine;
using Photon.Pun;

public class Sleigh : MonoBehaviour, IInteractable
{
    [Tooltip("순록들이 묶일 4개의 위치(Transform)")]
    public Transform[] attachmentPoints = new Transform[4];
    private PhotonView photonView;
    private int[] attachedReindeerIDs = new int[4];

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    // --- IInteractable 인터페이스 구현 ---

    // 요청을 받아 처리할 RPC 함수를 새로 추가합니다.
    [PunRPC]
    public void RequestAttachReindeerRPC(int reindeerViewID)
    {
        // 이 RPC는 방장만 실행합니다.
        if (!PhotonNetwork.IsMasterClient) return;

        // ViewID로 순록을 찾습니다.
        ReindeerController reindeer = PhotonView.Find(reindeerViewID)?.GetComponent<ReindeerController>();
        if (reindeer != null)
        {
            // 기존의 묶는 로직을 실행합니다.
            AttachReindeer(reindeer);
        }
    }
    public InteractionType InteractionType => InteractionType.Instant;

    public bool CanInteract(GameObject interactor)
    {
        // 상호작용하는 대상이 산타이고, 그 산타가 순록을 잡고 있을 때만 상호작용 가능
        SantaController santa = interactor.GetComponent<SantaController>();
        return (santa != null && santa.HasCapturedReindeer());
    }

    public string GetInteractMessage(GameObject interactor)
    {
        return "F to Attach Reindeer";
    }

    public bool Interact(GameObject interactor)
    {
        SantaController santa = interactor.GetComponent<SantaController>();
        if (santa != null && santa.HasCapturedReindeer())
        {
            // 직접 AttachReindeer를 호출하는 대신, 방장에게 RPC로 요청합니다.
            // 순록의 ViewID를 함께 보내줍니다.
            photonView.RPC("RequestAttachReindeerRPC", RpcTarget.MasterClient, santa.GetCapturedReindeer().GetComponent<PhotonView>().ViewID);
        }
        return true;
    }
    // --- 기존 썰매 로직 ---

    private void AttachReindeer(ReindeerController reindeer)
    {
        int emptySlotIndex = -1;
        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            if (attachedReindeerIDs[i] == 0)
            {
                emptySlotIndex = i;
                break;
            }
        }
        if (emptySlotIndex == -1) return;

        PhotonView reindeerPhotonView = reindeer.GetComponent<PhotonView>();
        if (reindeerPhotonView != null)
        {
            photonView.RPC("SyncAttachment", RpcTarget.AllBuffered, reindeerPhotonView.ViewID, emptySlotIndex);
        }
    }

    [PunRPC]
    private void SyncAttachment(int reindeerViewID, int slotIndex)
    {
        attachedReindeerIDs[slotIndex] = reindeerViewID;
        PhotonView reindeerPhotonView = PhotonView.Find(reindeerViewID);
        if (reindeerPhotonView != null)
        {
            reindeerPhotonView.RPC("AttachToSleigh", RpcTarget.All, photonView.ViewID, slotIndex);
        }
    }
}
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
        if (santa != null)
        {
            // 산타가 잡고 있는 순록 정보를 가져와서 묶기 로직을 실행
            AttachReindeer(santa.GetCapturedReindeer());
        }
        return true;
    }

    // --- 기존 썰매 로직 ---

    public void AttachReindeer(ReindeerController reindeer)
    {
        if (!PhotonNetwork.IsMasterClient || reindeer == null) return;

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
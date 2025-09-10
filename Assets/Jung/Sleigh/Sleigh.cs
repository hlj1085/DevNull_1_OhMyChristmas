using UnityEngine;
using Photon.Pun;

// [추가] IInteractable 인터페이스를 구현
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

    public InteractionType InteractionType => InteractionType.Instant; // 즉시 발동

    public bool CanInteract(GameObject interactor)
    {
        // "Player" 태그를 가진 게임 오브젝트를 찾습니다.
        GameObject santaObject = GameObject.FindGameObjectWithTag("Player");

        if (santaObject != null)
        {
            // 찾은 오브젝트에서 SantaController 스크립트를 가져옵니다.
            SantaController santa = santaObject.GetComponent<SantaController>();
            if (santa != null && santa.HasCapturedReindeer())
            {
                return true;
            }
        }

        return false;
    }

    public string GetInteractMessage(GameObject interactor)
    {
        return "F to Attach Reindeer";
    }

    // 산타가 썰매와 상호작용했을 때 호출되는 함수
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
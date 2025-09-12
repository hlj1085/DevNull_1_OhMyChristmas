using UnityEngine;
using Photon.Pun;

// 상호작용이 가능하도록 IInteractable 인터페이스를 구현합니다.
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Collider))]
public class ItemObject : MonoBehaviour, IInteractable
{
    [Tooltip("이 아이템의 데이터 (ScriptableObject)")]
    public ItemData itemData;

    private PhotonView photonView;
    private bool isPickedUp = false; // 중복 줍기 방지를 위한 스위치

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        // 콜라이더가 트리거 모드인지 확인 (상호작용에 필수)
        GetComponent<Collider>().isTrigger = true;
    }

    // --- IInteractable 인터페이스 구현 ---

    public InteractionType InteractionType => InteractionType.Instant;

    public bool CanInteract(GameObject interactor)
    {
        // 아직 줍지 않았고, 상호작용 대상이 순록일 때만 가능
        return !isPickedUp && interactor.CompareTag("Reindeer");
    }

    public string GetInteractMessage(GameObject interactorObject)
    {
        return $"F - {itemData.itemName} 줍기";
    }

    // --- [핵심 수정] ---
    // 플레이어가 상호작용했을 때, 직접 아이템을 추가하거나 파괴하지 않고 방장에게 '요청'만 보냅니다.
    public bool Interact(GameObject interactorObject)
    {
        if (isPickedUp) return false;

        // 즉시 상태를 변경하여 다른 플레이어가 동시에 줍는 것을 방지
        isPickedUp = true;

        // 아이템을 주운 플레이어의 ViewID를 가져옵니다.
        int pickuperViewID = interactorObject.GetComponent<PhotonView>().ViewID;

        // 방장에게 "저를 파괴하고 이 플레이어에게 아이템을 주세요" 라고 요청하는 RPC를 보냅니다.
        photonView.RPC("RequestPickupRPC", RpcTarget.MasterClient, pickuperViewID);

        return true; // 일단 상호작용은 성공한 것으로 처리
    }

    // --- [핵심 수정] ---
    // 방장만 실행하는 RPC 함수를 새로 추가합니다.
    [PunRPC]
    private void RequestPickupRPC(int pickuperViewID)
    {
        // 이 함수는 방장(Master Client)만 실행합니다.
        if (!PhotonNetwork.IsMasterClient) return;

        // 아이템을 주운 플레이어를 찾습니다.
        PhotonView pickuperView = PhotonView.Find(pickuperViewID);
        if (pickuperView != null && pickuperView.IsMine) // 로컬 플레이어인지 확인 (방장이 자기 자신일 경우)
        {
            // 로컬 인벤토리에 직접 추가
            var inventory = pickuperView.GetComponent<Inventory>();
            if (inventory != null) inventory.AddItem(itemData);
        }
        else if (pickuperView != null) // 다른 플레이어일 경우
        {
            // 해당 플레이어에게만 아이템을 추가하라고 RPC로 명령합니다.
            pickuperView.RPC("AddItemByNameRPC", pickuperView.Owner, itemData.name);
        }

        Debug.Log($"{pickuperView.Owner.NickName}에게 {itemData.name} 아이템 지급을 명령했습니다.");

        // 모든 네트워크에서 이 아이템 오브젝트를 안전하게 파괴합니다.
        PhotonNetwork.Destroy(this.gameObject);
    }
}
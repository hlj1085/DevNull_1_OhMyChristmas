using UnityEngine;
using Photon.Pun;

// 상호작용이 가능하도록 IInteractable 인터페이스를 구현합니다.
[RequireComponent(typeof(PhotonView))]
public class ItemObject : MonoBehaviour, IInteractable
{
    [Tooltip("이 아이템의 데이터 (ScriptableObject)")]
    public ItemData itemData;

    private PhotonView photonView;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    // --- IInteractable 인터페이스 구현 ---

    // 아이템 줍기는 즉시 발동
    public InteractionType InteractionType => InteractionType.Instant;

    // 항상 상호작용 가능
    public bool CanInteract => true;

    public string GetInteractMessage()
    {
        return "[F] to Get " + itemData.itemName;
    }

    // 플레이어가 상호작용했을 때 호출되는 함수
    // Interact 함수의 반환 타입을 bool로 변경하고, 성공 여부를 return
    public bool Interact(Inventory interactorInventory)
    {
        bool success = interactorInventory.AddItem(itemData);
        if (success)
        {
            photonView.RPC("DestroyItemRPC", RpcTarget.All);
        }
        return success; // 성공했으면 true, 인벤토리가 꽉 찼으면 false 반환
    }

    // --- RPC 함수 ---

    [PunRPC]
    private void DestroyItemRPC()
    {
        // 이 신호를 받은 모든 클라이언트는 자신의 씬에 있는 이 아이템 오브젝트를 파괴합니다.
        Destroy(this.gameObject);
    }
}
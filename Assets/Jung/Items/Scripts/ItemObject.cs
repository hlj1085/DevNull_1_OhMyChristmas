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
    public void Interact(Inventory interactorInventory)
    {
        // 1. 상호작용한 플레이어의 인벤토리에 아이템을 추가해 봅니다.
        bool success = interactorInventory.AddItem(itemData);

        // 2. 인벤토리에 아이템이 성공적으로 추가되었다면 (인벤토리가 꽉 차지 않았다면)
        if (success)
        {
            // 3. 모든 클라이언트에게 이 아이템을 파괴하라는 RPC를 보냅니다.
            photonView.RPC("DestroyItemRPC", RpcTarget.All);
        }
    }

    // --- RPC 함수 ---

    [PunRPC]
    private void DestroyItemRPC()
    {
        // 이 신호를 받은 모든 클라이언트는 자신의 씬에 있는 이 아이템 오브젝트를 파괴합니다.
        Destroy(this.gameObject);
    }
}
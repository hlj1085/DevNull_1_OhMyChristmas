using Photon.Pun;
using UnityEngine;

public class Fairy : MonoBehaviour, IInteractable
{
    [Tooltip("상호작용 시 확인할 요정 가루 ItemData")]
    public ItemData fairyDustItemData;

    [Tooltip("상호작용 시 표시될 메시지")]
    public string interactMessage = "F - 요정에게 요정가루 바치기";

    // 상호작용 방식은 '꾹 누르기' 입니다.
    public InteractionType InteractionType => InteractionType.Hold;

    /// <summary>
    /// 이 오브젝트와 상호작용할 수 있는지 여부를 반환합니다.
    /// </summary>
    public bool CanInteract(GameObject interactor)
    {
        // 상호작용을 시도하는 오브젝트가 "Reindeer" 태그를 가지고 있는지 확인합니다.
        if (!interactor.CompareTag("Reindeer")) return false;

        // 순록의 인벤토리를 가져와 요정가루를 가지고 있는지 확인합니다.
        Inventory interactorInventory = interactor.GetComponent<Inventory>();
        if (interactorInventory != null && interactorInventory.HasItem(fairyDustItemData))
        {
            return true; // 요정가루가 있으면 상호작용 가능
        }

        return false; // 요정가루가 없으면 상호작용 불가능
    }

    /// <summary>
    /// 상호작용 UI에 표시될 텍스트를 반환합니다.
    /// </summary>
    public string GetInteractMessage(GameObject interactor)
    {
        // 요정가루를 가지고 있을 때만 메시지를 보여줍니다.
        if (CanInteract(interactor))
        {
            return interactMessage;
        }
        return "요정가루가 없습니다";
    }

    /// <summary>
    /// 상호작용을 실행합니다.
    /// </summary>
    /// <returns>상호작용 성공 여부</returns>
    public bool Interact(GameObject interactorObject)
    {
        // 순록의 인벤토리와 PhotonView 컴포넌트를 가져옵니다.
        Inventory interactorInventory = interactorObject.GetComponent<Inventory>();
        PhotonView interactorView = interactorObject.GetComponent<PhotonView>();

        // 두 컴포넌트가 모두 존재하고, 순록이 요정가루를 가지고 있는지 다시 확인합니다.
        if (interactorInventory != null && interactorView != null && interactorInventory.HasItem(fairyDustItemData))
        {
            // [네트워크 동기화] 모든 클라이언트에게 해당 순록의 인벤토리에서 요정가루 아이템을 제거하라고 명령합니다.
            interactorView.RPC("RemoveItemByNameRPC", RpcTarget.All, fairyDustItemData.name);

            // [네트워크 동기화] FairyManager에 요정가루 1개를 추가하라고 명령합니다.
            FairyManager.instance.AddDustNetworked(1);

            Debug.Log($"[Fairy] {interactorObject.name}이(가) 요정가루 1개를 바쳤습니다.");

            return true; // 상호작용 성공
        }

        Debug.LogWarning($"[Fairy] {interactorObject.name}이(가) 요정가루가 없어 상호작용에 실패했습니다.");
        return false; // 상호작용 실패
    }
}
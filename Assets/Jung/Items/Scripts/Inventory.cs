using System.Collections.Generic;
using UnityEngine;
using System; // [추가] Action을 사용하기 위해 필요합니다.

public class Inventory : MonoBehaviour
{
    // [추가] 인벤토리가 변경되었을 때 외부로 알리는 방송국(이벤트)
    public event Action OnInventoryChanged;

    [SerializeField]
    private List<ItemData> items = new List<ItemData>();
    public int inventorySlotLimit = 3;

    // [추가] UI가 인벤토리의 현재 아이템 목록을 읽어갈 수 있도록 해주는 기능
    public List<ItemData> GetItems()
    {
        return items;
    }

    public bool AddItem(ItemData itemToAdd)
    {
        if (items.Count >= inventorySlotLimit)
        {
            Debug.Log("인벤토리가 꽉 찼습니다!");
            return false;
        }

        items.Add(itemToAdd);
        Debug.Log(itemToAdd.itemName + "을(를) 획득했다!");

        // [추가] "아이템 추가됐다!" 라고 신호를 보냅니다.
        OnInventoryChanged?.Invoke();

        return true;
    }

    public void RemoveItem(ItemData itemToRemove)
    {
        if (items.Contains(itemToRemove))
        {
            items.Remove(itemToRemove);
            // [추가] "아이템 제거됐다!" 라고 신호를 보냅니다.
            OnInventoryChanged?.Invoke();
        }
    }
}
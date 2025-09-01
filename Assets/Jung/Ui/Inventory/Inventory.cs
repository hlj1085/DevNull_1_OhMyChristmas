using System.Collections.Generic;
using UnityEngine;
using System;

public class Inventory : MonoBehaviour
{
    public event Action OnInventoryChanged;

    [SerializeField]
    private List<ItemData> items = new List<ItemData>();
    public int inventorySlotLimit = 3;

    // --- [추가] --- Awake에서 인벤토리를 빈 슬롯으로 초기화합니다.
    private void Awake()
    {
        // 인벤토리 리스트를 슬롯 한도만큼 null 값으로 채워서 초기화합니다.
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            items.Add(null);
        }
    }

    public List<ItemData> GetItems()
    {
        return items;
    }

    // --- [수정] --- 빈칸을 찾아 아이템을 추가하도록 변경
    public bool AddItem(ItemData itemToAdd)
    {
        // 인벤토리의 모든 슬롯을 확인
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            // 만약 비어있는 슬롯(null)을 찾았다면
            if (items[i] == null)
            {
                // 그 자리에 아이템을 추가
                items[i] = itemToAdd;
                Debug.Log(itemToAdd.itemName + $"을(를) {i + 1}번 슬롯에 획득했다!");

                // "인벤토리 변경됐다!" 라고 신호를 보냄
                OnInventoryChanged?.Invoke();
                return true; // 추가 성공
            }
        }

        // 모든 슬롯을 확인했는데도 빈칸이 없으면
        Debug.Log("인벤토리가 꽉 찼습니다!");
        return false; // 추가 실패
    }

    // --- [수정] --- 리스트에서 요소를 지우는 대신 null로 만드는 방식으로 변경
    public void RemoveItem(ItemData itemToRemove)
    {
        // 인벤토리의 모든 슬롯을 확인
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            // 만약 제거하려는 아이템을 찾았다면
            if (items[i] == itemToRemove)
            {
                // 리스트에서 완전히 제거하는 대신, 그 칸을 null(빈칸)으로 만듦
                items[i] = null;
                Debug.Log(itemToRemove.itemName + $"을(를) {i + 1}번 슬롯에서 제거했다!");

                // "인벤토리 변경됐다!" 라고 신호를 보냄
                OnInventoryChanged?.Invoke();
                return; // 제거 완료
            }
        }
    }
}
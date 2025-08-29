using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;

    [Header("UI 요소")]
    public GameObject inventoryPanel; // 인벤토리 전체 패널 (선택사항)
    public Image[] itemSlots;       // 인벤토리 슬롯 역할을 할 이미지 배열

    // 이 스크립트가 활성화될 때마다 인벤토리의 변경 이벤트를 구독(수신 대기)합니다.
    void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += UpdateUI; // UpdateUI 함수를 이벤트에 등록
            UpdateUI(); // 활성화될 때 현재 상태로 한 번 업데이트
        }
    }

    // 이 스크립트가 비활성화될 때 이벤트 구독을 취소합니다. (메모리 누수 방지)
    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= UpdateUI; // 이벤트에서 등록 해제
        }
    }

    // Update 함수는 더 이상 필요 없으므로 삭제합니다.

    /// <summary>
    /// 인벤토리의 현재 상태를 기반으로 UI를 새로고침하는 함수입니다.
    /// </summary>
    void UpdateUI()
    {
        if (inventory == null || itemSlots == null) return;

        // 현재 인벤토리에 있는 아이템 목록을 가져옵니다.
        List<ItemData> items = inventory.GetItems();

        // 모든 UI 슬롯을 순회합니다.
        for (int i = 0; i < itemSlots.Length; i++)
        {
            // 만약 현재 슬롯 번호에 해당하는 아이템이 인벤토리에 있다면
            if (i < items.Count)
            {
                // 슬롯 이미지를 해당 아이템의 아이콘으로 바꾸고, 보이게 합니다.
                itemSlots[i].sprite = items[i].itemIcon;
                itemSlots[i].enabled = true;
            }
            // 해당하는 아이템이 없다면 (슬롯이 비어있다면)
            else
            {
                // 슬롯 이미지를 비우고, 보이지 않게 합니다.
                itemSlots[i].sprite = null;
                itemSlots[i].enabled = false;
            }
        }
    }
}
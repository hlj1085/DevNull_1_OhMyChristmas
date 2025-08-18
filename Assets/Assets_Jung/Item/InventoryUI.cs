using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public Inventory targetInventory; // 플레이어의 인벤토리 정보를 가져올 대상
    public Image[] slots;             // UI 슬롯 이미지들

    void Start()
    {
        // 인벤토리의 'OnInventoryChanged' 방송을 구독(Subscribe)합니다.
        // 즉, "인벤토리에 변화가 생기면 UpdateUI 함수를 실행해줘" 라고 등록하는 것입니다.
        if (targetInventory != null)
        {
            targetInventory.OnInventoryChanged += UpdateUI;
        }

        // 게임 시작 시 UI를 한 번 초기화합니다.
        UpdateUI();
    }

    private void OnDestroy()
    {
        // 이 UI 오브젝트가 파괴될 때, 구독을 해지해서 메모리 누수를 방지합니다.
        if (targetInventory != null)
        {
            targetInventory.OnInventoryChanged -= UpdateUI;
        }
    }

    // UI를 새로 그리는 함수
    private void UpdateUI()
    {
        List<ItemData> items = targetInventory.GetItems();

        // 모든 슬롯을 순회합니다.
        for (int i = 0; i < slots.Length; i++)
        {
            // 만약 현재 슬롯 순서에 해당하는 아이템이 인벤토리에 있다면,
            if (i < items.Count)
            {
                // 슬롯 이미지에 아이템 아이콘을 표시하고, 불투명하게 만듭니다.
                slots[i].sprite = items[i].itemIcon;
                slots[i].color = new Color(1, 1, 1, 1);
            }
            else
            {
                // 해당하는 아이템이 없다면, 아이콘을 없애고 투명하게 만듭니다.
                slots[i].sprite = null;
                slots[i].color = new Color(1, 1, 1, 0);
            }
        }
    }
}
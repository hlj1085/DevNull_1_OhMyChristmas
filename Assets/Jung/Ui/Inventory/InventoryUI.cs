using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    // inventory 변수를 private으로 바꾸고, 외부 접근을 위한 public 프로퍼티를 만듭니다.
    private Inventory _inventory;
    public Inventory inventory
    {
        get { return _inventory; }
        set
        {
            // 만약 이미 연결된 인벤토리가 있다면, 이벤트 구독을 먼저 해제합니다.
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= UpdateUI;
            }

            // 새로운 인벤토리를 할당합니다.
            _inventory = value;

            // 새로 할당된 인벤토리가 있다면, 그 인벤토리의 이벤트를 구독합니다.
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += UpdateUI;
            }

            // 인벤토리가 연결되거나 해제될 때마다 UI를 즉시 업데이트합니다.
            UpdateUI();
        }
    }

    [Header("UI 슬롯 설정")]
    [Tooltip("아이템 아이콘을 표시할 Image 컴포넌트 배열 (Item1/Item, Item2/Item, Item3/Item)")]
    public Image[] itemIcons;

    [Tooltip("선택 시 활성화될 테두리 GameObject 배열 (Item_Border1, Item_Border2, Item_Border3)")]
    public GameObject[] selectionBorders;

    /// <summary>
    /// 인벤토리의 현재 상태를 기반으로 UI를 새로고침하는 함수입니다.
    /// </summary>
    void UpdateUI()
    {
        if (_inventory == null || itemIcons == null) return;

        List<ItemData> items = _inventory.GetItems();

        // 모든 아이콘 슬롯을 순회합니다.
        for (int i = 0; i < itemIcons.Length; i++)
        {
            // 현재 슬롯(i)에 아이템이 존재하는 경우
            if (i < items.Count && items[i] != null)
            {
                // 아이콘 이미지(sprite)를 아이템 데이터의 아이콘으로 변경
                itemIcons[i].sprite = items[i].itemIcon;
                // 아이콘 게임 오브젝트를 활성화하여 화면에 보이게 함
                itemIcons[i].gameObject.SetActive(true);
            }
            // 현재 슬롯(i)이 비어있는 경우
            else
            {
                // 아이콘 게임 오브젝트를 비활성화하여 화면에서 숨김
                itemIcons[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 선택된 인벤토리 슬롯 테두리를 표시합니다.
    /// </summary>
    /// <param name="slotIndex">선택된 슬롯 번호. 선택 해제 시 -1.</param>
    public void UpdateSelection(int slotIndex)
    {
        if (selectionBorders == null) return;

        // 모든 테두리를 순회합니다.
        for (int i = 0; i < selectionBorders.Length; i++)
        {
            if (selectionBorders[i] != null)
            {
                // 현재 순번(i)이 선택된 슬롯 번호(slotIndex)와 같으면 테두리를 켜고,
                // 그렇지 않으면 끕니다.
                selectionBorders[i].SetActive(i == slotIndex);
            }
        }
    }
}
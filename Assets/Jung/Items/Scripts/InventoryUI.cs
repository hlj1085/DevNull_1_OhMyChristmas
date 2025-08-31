using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public Image[] slotSelectionBorders; // 선택 테두리 이미지 배열

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

    [Header("UI 요소")]
    public GameObject inventoryPanel;
    public Image[] itemSlots;

    // OnEnable과 OnDisable은 이제 프로퍼티에서 모든 것을 처리하므로 필요 없습니다.
    // void OnEnable() { ... }
    // void OnDisable() { ... }

    /// <summary>
    /// 인벤토리의 현재 상태를 기반으로 UI를 새로고침하는 함수입니다.
    /// </summary>
    void UpdateUI()
    {
        // inventory 대신 _inventory를 사용합니다.
        if (_inventory == null || itemSlots == null) return;

        List<ItemData> items = _inventory.GetItems();

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (i < items.Count && items[i] != null) // 아이템이 null이 아닌지도 확인
            {
                itemSlots[i].sprite = items[i].itemIcon;
                itemSlots[i].enabled = true;
            }
            else
            {
                itemSlots[i].sprite = null;
                itemSlots[i].enabled = false;
            }
        }
    }
    /// <summary>
    /// 선택된 인벤토리 슬롯 테두리를 표시합니다.
    /// </summary>
    /// <param name="slotIndex">선택된 슬롯 번호. 선택 해제 시 -1.</param>
    public void UpdateSelection(int slotIndex)
    {
        // 모든 테두리를 순회합니다.
        for (int i = 0; i < slotSelectionBorders.Length; i++)
        {
            if (slotSelectionBorders[i] != null)
            {
                // 현재 순번(i)이 선택된 슬롯 번호(slotIndex)와 같으면 테두리를 켜고,
                // 그렇지 않으면 끕니다.
                slotSelectionBorders[i].gameObject.SetActive(i == slotIndex);
            }
        }
    }
}
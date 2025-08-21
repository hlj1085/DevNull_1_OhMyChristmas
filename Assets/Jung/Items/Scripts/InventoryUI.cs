using UnityEngine;
using UnityEngine.UI; // UI 관련 기능을 사용하려면 추가해야 할 수 있습니다.

public class InventoryUI : MonoBehaviour
{
    // --- [핵심] --- 
    // UIManager가 이 변수에 접근해서 값을 채워줄 수 있도록 public으로 선언해야 합니다.
    public Inventory inventory;

    // ... (인벤토리 슬롯, 아이템 이미지 등 다른 UI 관련 변수들)
    public GameObject inventoryPanel;
    public Image[] itemSlots; // 예시

    void Start()
    {
        // 처음에는 인벤토리 UI를 숨깁니다.
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    void Update()
    {
        // 인벤토리가 연결되었는지 확인하고 UI를 업데이트합니다.
        if (inventory == null)
        {
            // 이 부분이 NullReferenceException을 발생시키던 원인입니다.
            // 이제 inventory가 할당될 때까지 아무것도 하지 않으므로 안전합니다.
            return;
        }

        // 여기에 인벤토리 내용을 실제 UI에 표시하는 코드를 작성합니다.
        // 예: for문으로 아이템 슬롯을 순회하며 이미지 변경 등
        UpdateUI();
    }

    public void UpdateUI()
    {
        // 이 함수가 인벤토리의 실제 데이터를 UI에 표시하는 역할을 합니다.
        // inventory 변수가 null이 아닐 때만 호출되어야 안전합니다.
        if (inventory == null) return;
        // 예시: for (int i = 0; i < itemSlots.Length; i++) { ... }
    }
}
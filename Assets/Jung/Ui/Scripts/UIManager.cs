using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("UI Groups")]
    public GameObject interactionUIGroup;

    public GameObject recoveryUIGroup;
    public GameObject gameStatusUIGroup;
    public GameObject inventoryUIGroup;

    [Header("UI Elements")]
    public TextMeshProUGUI interactionPromptUI;
    public TextMeshProUGUI useItemPromptUI;
    public TextMeshProUGUI recoveryText; // <<< [추가] 회복 바 텍스트

    public Slider interactionSlider;
    public Slider recoverySlider;
    public Image recoverySliderFill; // <<< [추가] 회복 바의 Fill 이미지


    // --- [수정] --- InventoryUI를 외부에서 연결할 수 있도록 public 변수로 변경
    [Header("UI Scripts")]
    public InventoryUI inventoryUI;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // --- [삭제] --- 더 이상 GetComponent로 찾을 필요가 없으므로 이 부분은 삭제합니다.
        // inventoryUI = GetComponent<InventoryUI>(); 
        // if (inventoryUI == null)
        // {
        //     Debug.LogWarning("UIManager 게임 오브젝트에 InventoryUI 스크립트가 없습니다!");
        // }
    }

    /// <summary>
    /// 플레이어의 인벤토리를 UI에 연결하는 함수입니다.
    /// </summary>
    public void SetInventory(Inventory targetInventory)
    {
        if (inventoryUI != null)
        {
            // 인스펙터에서 연결된 inventoryUI에게 목표 인벤토리를 알려줍니다.
            inventoryUI.inventory = targetInventory;        }
        else
        {
            Debug.LogError("UIManager에 InventoryUI가 연결되지 않아 인벤토리를 설정할 수 없습니다!");
        }
    }
}
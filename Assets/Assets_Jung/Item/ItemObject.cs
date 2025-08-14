using UnityEngine;

public class ItemObject : MonoBehaviour, IInteractable
{
    [Header("아이템 정보")]
    public ItemData itemData;

    [Header("효과")]
    public float rotationSpeed = 50f;
    public float floatSpeed = 0.5f;
    public float floatHeight = 0.5f;

    private Vector3 startPosition;

    // [추가] 현재 수집 절차가 진행 중인지 확인하는 상태 변수
    private bool isBeingCollected = false;

    // [수정] 수집 중일 때는 상호작용이 불가능하도록 규칙 변경
    public bool CanInteract => !isBeingCollected;
    public InteractionType InteractionType => InteractionType.Instant;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    public string GetInteractMessage()
    {
        return itemData != null ? $"Press [F] to pick up {itemData.itemName}" : "";
    }

    public void Interact(Inventory inventory)
    {
        // 이미 수집 절차가 시작됐으면, 중복 실행을 막습니다.
        if (isBeingCollected) return;
        if (itemData == null || inventory == null) return;

        // 이제부터 수집 절차를 시작한다고 알립니다.
        isBeingCollected = true;

        // 인벤토리에 아이템 추가를 시도합니다.
        if (inventory.AddItem(itemData))
        {
            // 성공했다면, 아이템 오브젝트를 파괴합니다.
            Destroy(gameObject);
        }
        else
        {
            // [추가] 실패했다면 (인벤토리가 꽉 찼다면), 
            // 다시 주울 수 있도록 상태를 원래대로 되돌립니다.
            isBeingCollected = false;
        }
    }
}
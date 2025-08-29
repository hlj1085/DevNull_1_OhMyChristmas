using UnityEngine;
using System.Collections.Generic; // List를 사용하기 위해 추가

// ItemType Enum은 더 이상 필요 없으므로 삭제해도 됩니다.

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName;
    public Sprite itemIcon;
    public GameObject itemPrefab;

    [Header("아이템 효과")]
    [Tooltip("이 아이템을 사용했을 때 발동될 효과들을 여기에 연결하세요.")]
    public List<ItemEffect> effects; // 아이템 효과 부품들을 담을 리스트

    // 아이템 사용 함수
    public void Use(ReindeerController user)
    {
        // 연결된 모든 효과들을 차례대로 실행
        foreach (var effect in effects)
        {
            effect.ExecuteEffect(user);
        }
    }
}
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("아이템 정보")]
    public string itemName;

    [TextArea]
    public string itemDescription;

    public Sprite itemIcon;
}
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField]
    private List<ItemData> items = new List<ItemData>();

    public int inventorySlotLimit = 3;

    public bool AddItem(ItemData itemToAdd)
    {
        if (items.Count >= inventorySlotLimit)
        {
            Debug.Log("¿Œ∫•≈‰∏Æ∞° ≤À √°Ω¿¥œ¥Ÿ!");
            return false;
        }

        items.Add(itemToAdd);
        Debug.Log(itemToAdd.itemName + "¿ª(∏¶) »πµÊ«ﬂ¥Ÿ!");
        return true;
    }

    public void RemoveItem(ItemData itemToRemove)
    {
        if (items.Contains(itemToRemove))
        {
            items.Remove(itemToRemove);
        }
    }
}
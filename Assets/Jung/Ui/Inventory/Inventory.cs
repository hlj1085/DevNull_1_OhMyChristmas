using System.Collections.Generic;
using UnityEngine;
using System;
using Photon.Pun; // Photon 기능을 사용하기 위해 추가

[RequireComponent(typeof(PhotonView))] // PhotonView를 필수로 만듦
public class Inventory : MonoBehaviour
{
    public event Action OnInventoryChanged;

    [SerializeField]
    private List<ItemData> items = new List<ItemData>();
    public int inventorySlotLimit = 3;

    private PhotonView photonView; // RPC 호출을 위한 PhotonView 변수

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();

        // 인벤토리 리스트를 슬롯 한도만큼 null 값으로 채워서 초기화
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            items.Add(null);
        }
    }

    public List<ItemData> GetItems()
    {
        return items;
    }

    // --- [수정] --- 이제 이 함수는 RPC를 호출하는 역할만 합니다.
    public bool AddItem(ItemData itemToAdd)
    {
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            if (items[i] == null)
            {
                // 직접 아이템을 추가하는 대신, 모든 클라이언트에게 추가하라는 RPC를 보냅니다.
                photonView.RPC("AddItemRPC", RpcTarget.All, itemToAdd.name, i);
                return true;
            }
        }

        Debug.Log("인벤토리가 꽉 찼습니다!");
        return false;
    }

    // --- [수정] --- 이제 이 함수는 RPC를 호출하는 역할만 합니다.
    public void RemoveItem(ItemData itemToRemove)
    {
        for (int i = 0; i < inventorySlotLimit; i++)
        {
            if (items[i] == itemToRemove)
            {
                // 직접 아이템을 제거하는 대신, 모든 클라이언트에게 제거하라는 RPC를 보냅니다.
                photonView.RPC("RemoveItemRPC", RpcTarget.All, i);
                return;
            }
        }
    }

    // --- [추가] --- 모든 클라이언트에서 실행될 RPC 함수들
    [PunRPC]
    private void AddItemRPC(string itemName, int slotIndex)
    {
        ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
        if (itemData != null)
        {
            items[slotIndex] = itemData;
            // 모든 클라이언트의 UI를 갱신하기 위해 이벤트를 호출합니다.
            OnInventoryChanged?.Invoke();
        }
    }

    [PunRPC]
    private void RemoveItemRPC(int slotIndex)
    {
        if (items[slotIndex] != null)
        {
            items[slotIndex] = null;

            // 모든 클라이언트의 UI를 갱신하기 위해 이벤트를 호출합니다.
            OnInventoryChanged?.Invoke();
        }
    }

    public bool HasItem(ItemData itemToCheck)
    {
        foreach (ItemData item in items)
        {
            if (item != null && item.itemName == itemToCheck.itemName)
            {
                return true;
            }
        }
        return false;
    }
}
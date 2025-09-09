// MultiManager.cs
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class MultiManager : MonoBehaviour
{
    public GameObject santaPrefab;
    public GameObject reindeerPrefab;
    public Transform santaSpawnPoint;
    public List<Transform> reindeerSpawnPoints;

    void Start()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        string role = "Reindeer";
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object roleObj))
        {
            role = roleObj as string;
        }

        // --- [핵심] --- UIManager에게 내 역할을 알려 UI를 설정하도록 함
        if (UIManager.instance != null)
        {
            UIManager.instance.InitializeUIForRole(role);
        }

        GameObject prefab = role == "Santa" ? santaPrefab : reindeerPrefab;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (role == "Santa")
        {
            if (santaSpawnPoint != null)
            {
                spawnPosition = santaSpawnPoint.position;
                spawnRotation = santaSpawnPoint.rotation;
            }
        }
        else // Reindeer
        {
            int reindeerIndex = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.CustomProperties.TryGetValue("Role", out object playerRole) && (string)playerRole == "Reindeer")
                {
                    if (player.IsLocal) break;
                    reindeerIndex++;
                }
            }
            int spawnIndex = reindeerIndex % reindeerSpawnPoints.Count;
            if (reindeerSpawnPoints.Count > spawnIndex && reindeerSpawnPoints[spawnIndex] != null)
            {
                spawnPosition = reindeerSpawnPoints[spawnIndex].position;
                spawnRotation = reindeerSpawnPoints[spawnIndex].rotation;
            }
        }

        // 플레이어 생성
        GameObject playerObject = PhotonNetwork.Instantiate(prefab.name, spawnPosition, spawnRotation);

        // --- [추가] --- 생성된 플레이어가 순록일 경우 인벤토리 UI 연결
        if (role == "Reindeer")
        {
            Inventory playerInventory = playerObject.GetComponent<Inventory>();
            // UIManager의 reindeerCanvas에서 InventoryUI를 찾아 연결
            InventoryUI inventoryUI = UIManager.instance.reindeerCanvas.GetComponentInChildren<InventoryUI>();

            if (UIManager.instance != null && playerInventory != null && inventoryUI != null)
            {
                UIManager.instance.SetInventory(playerInventory, inventoryUI);
            }
        }
    }
}
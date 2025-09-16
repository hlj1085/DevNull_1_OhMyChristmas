using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq; // Linq를 사용하기 위해 추가
using UnityEngine;


public class ItemSpawnManager : MonoBehaviour
{
    public static ItemSpawnManager instance;

    [Header("스폰 설정")]
    [Tooltip("생성할 상자 프리팹")]
    public GameObject chestPrefab;
    [Tooltip("상자가 생성될 수 있는 모든 위치")]
    public Transform spawnPointsParent; // <<< 이 변수를 새로 추가합니다.
    public Transform[] spawnPoints; // <<< 이 변수는 그대로 두지만, HideInInspector를 추가합니다.

    [Tooltip("실제로 생성할 상자의 개수")]
    public int numberOfChestsToSpawn = 20;

    [Header("아이템 분배 설정")]
    [Tooltip("이번 게임에 나올 모든 아이템 목록을 여기에 채워주세요.")]
    public List<ItemData> itemPool;
    [Tooltip("특별히 개수를 지정할 요정가루 아이템 데이터")]
    public ItemData fairyDustItemData; // <<< 이 변수를 새로 추가하세요!
    // 네트워크로 동기화될 최종 아이템 목록
    private List<string> shuffledItemNames = new List<string>();
    private int currentItemIndex = 0; // 아이템 분배를 위한 인덱스

    private void Awake()
    {
        // 싱글톤 패턴 (기존과 동일)
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return; // 중복 인스턴스일 경우, 아래 코드가 실행되지 않도록 return 추가
        }

        // --- [이 부분이 새로 추가됩니다] ---
        // spawnPointsParent가 연결되어 있는지 확인합니다.
        if (spawnPointsParent != null)
        {
            // 자식 오브젝트의 개수만큼 spawnPoints 배열의 크기를 설정합니다.
            spawnPoints = new Transform[spawnPointsParent.childCount];

            // 반복문을 통해 모든 자식 오브젝트의 Transform을 배열에 저장합니다.
            for (int i = 0; i < spawnPointsParent.childCount; i++)
            {
                spawnPoints[i] = spawnPointsParent.GetChild(i);
            }
            Debug.Log($"{spawnPoints.Length}개의 스폰 포인트를 자동으로 불러왔습니다.");
        }
        else
        {
            Debug.LogError("ItemSpawnManager에 'Spawn Points Parent'가 연결되지 않았습니다!");
        }
        // --- [추가 끝] ---
    }

    void Start()
    {
        // 오직 방장(Master Client)만 스폰 및 아이템 분배 로직을 실행합니다.
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("방장이 상자 위치와 아이템 목록을 설정합니다.");
            SetupChestsAndItems();
        }
    }
    void SetupChestsAndItems()
    {
        // 1. 상자 위치 랜덤화 (이 부분은 기존과 동일)
        List<Transform> availableSpawnPoints = spawnPoints.ToList();
        for (int i = 0; i < availableSpawnPoints.Count - 1; i++)
        {
            int rnd = Random.Range(i, availableSpawnPoints.Count);
            Transform temp = availableSpawnPoints[rnd];
            availableSpawnPoints[rnd] = availableSpawnPoints[i];
            availableSpawnPoints[i] = temp;
        }

        for (int i = 0; i < numberOfChestsToSpawn; i++)
        {
            if (i < availableSpawnPoints.Count)
            {
                Transform spawnPoint = availableSpawnPoints[i];
                float randomYRotation = Random.Range(0f, 360f);
                Quaternion spawnRotation = Quaternion.Euler(0f, randomYRotation, 0f);
                PhotonNetwork.Instantiate(chestPrefab.name, spawnPoint.position, spawnRotation);
            }
        }




        // --- 2. 새로운 규칙에 따라 아이템 목록 생성 ---
        List<string> itemNamesToGenerate = new List<string>();

        // 규칙 1: 요정가루 8개 추가
        string fairyDustPrefabName = fairyDustItemData.name.Replace("_Data", "_Prefab");
        for (int i = 0; i < 8; i++)
        {
            itemNamesToGenerate.Add(fairyDustPrefabName);
        }

        // 규칙 2: 나머지 아이템 찾기 (요정가루 제외)
        List<ItemData> otherItems = new List<ItemData>();
        foreach (var item in itemPool)
        {
            if (item != fairyDustItemData)
            {
                otherItems.Add(item);
            }
        }

        // 규칙 3: 남은 공간을 나머지 아이템으로 반반 채우기
        int remainingSlots = numberOfChestsToSpawn - 8;
        if (remainingSlots > 0 && otherItems.Count > 0)
        {
            int slotsPerItem = remainingSlots / otherItems.Count;
            int remainder = remainingSlots % otherItems.Count;

            foreach (var item in otherItems)
            {
                string prefabName = item.name.Replace("_Data", "_Prefab");
                int count = slotsPerItem;
                if (remainder > 0) // 남은 개수는 앞의 아이템부터 하나씩 더 채워줌
                {
                    count++;
                    remainder--;
                }

                for (int i = 0; i < count; i++)
                {
                    itemNamesToGenerate.Add(prefabName);
                }
            }
        }

        // --- 3. 생성된 목록을 무작위로 섞기 (Fisher-Yates Shuffle) ---
        for (int i = itemNamesToGenerate.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            string temp = itemNamesToGenerate[i];
            itemNamesToGenerate[i] = itemNamesToGenerate[rnd];
            itemNamesToGenerate[rnd] = temp;
        }

        // --- 4. 최종 목록을 모든 클라이언트와 동기화 ---
        GetComponent<PhotonView>().RPC("SyncShuffledItemListRPC", RpcTarget.All, itemNamesToGenerate.ToArray());

    }

    [PunRPC]
    private void SyncShuffledItemListRPC(string[] itemNames)
    {
        shuffledItemNames = itemNames.ToList();
        currentItemIndex = 0; // 인덱스 초기화
        Debug.Log("모든 클라이언트가 섞인 아이템 목록을 동기화했습니다. 총 아이템 수: " + shuffledItemNames.Count);
    }

    // 방장이 상자에게 아이템을 부여하는 RPC
    [PunRPC]
    private void RequestItemFromMasterRPC(int playerViewID)
    {
        // 방장만 이 로직을 실행합니다.
        if (!PhotonNetwork.IsMasterClient) return;

        // 아이템이 더 남아있는지 확인
        if (currentItemIndex < shuffledItemNames.Count)
        {
            string itemToGive = shuffledItemNames[currentItemIndex];
            currentItemIndex++; // 다음 아이템을 가리키도록 인덱스 증가

            PhotonView playerView = PhotonView.Find(playerViewID);
            if (playerView != null)
            {
                // 아이템을 요청한 플레이어에게만 아이템을 주라고 명령합니다.
                playerView.RPC("AddItemByNameRPC", playerView.Owner, itemToGive);
                Debug.Log($"{playerView.Owner.NickName}에게 {itemToGive} 아이템을 지급했습니다.");
            }
        }
        else
        {
            Debug.LogWarning("아이템 풀이 모두 소진되었습니다!");
        }
    }
    // Chest가 호출할 RPC 함수를 새로 만듭니다.
    [PunRPC]
    public void RequestItemSpawnRPC(int chestViewID)
    {
        // 이 RPC를 받은 사람이 방장일 경우에만 아이템을 생성합니다.
        if (PhotonNetwork.IsMasterClient)
        {
            RequestItemSpawn(chestViewID); // 기존 로직을 그대로 재사용
        }
    }
    private void RequestItemSpawn(int chestViewID)
    {
        if (currentItemIndex < shuffledItemNames.Count)
        {
            string itemPrefabNameToSpawn = shuffledItemNames[currentItemIndex];
            currentItemIndex++;

            PhotonView chestView = PhotonView.Find(chestViewID);
            if (chestView != null)
            {
                // 소환할 프리팹의 전체 경로를 지정합니다. (Resources 폴더 기준)
                string prefabPath = $"Items/{itemPrefabNameToSpawn}";

                // 상자의 스폰 위치를 가져옵니다.
                Transform spawnPoint = chestView.GetComponent<Chest>().itemSpawnPoint;

                // 방장이 직접 '네트워크 아이템'으로 생성합니다.
                PhotonNetwork.Instantiate(prefabPath, spawnPoint.position, spawnPoint.rotation);
            }
        }
        else
        {
            Debug.LogWarning("아이템 풀이 모두 소진되었습니다!");
        }
    }

}   
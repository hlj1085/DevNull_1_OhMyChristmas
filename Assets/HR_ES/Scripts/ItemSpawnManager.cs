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
    public Transform[] spawnPoints;
    [Tooltip("실제로 생성할 상자의 개수")]
    public int numberOfChestsToSpawn = 20;

    [Header("아이템 분배 설정")]
    [Tooltip("이번 게임에 나올 모든 아이템 목록을 여기에 채워주세요.")]
    public List<ItemData> itemPool;

    // 네트워크로 동기화될 최종 아이템 목록
    private List<string> shuffledItemNames = new List<string>();
    private int currentItemIndex = 0; // 아이템 분배를 위한 인덱스

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
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
        // 1. 상자 위치 랜덤화
        List<Transform> availableSpawnPoints = spawnPoints.ToList();
        // Fisher-Yates 셔플 알고리즘으로 스폰 위치를 무작위로 섞습니다.
        for (int i = 0; i < availableSpawnPoints.Count - 1; i++)
        {
            int rnd = Random.Range(i, availableSpawnPoints.Count);
            Transform temp = availableSpawnPoints[rnd];
            availableSpawnPoints[rnd] = availableSpawnPoints[i];
            availableSpawnPoints[i] = temp;
        }

        // 섞인 위치 목록에서 정해진 개수만큼 상자를 네트워크상에 생성합니다.
        for (int i = 0; i < numberOfChestsToSpawn; i++)
        {
            if (i < availableSpawnPoints.Count)
            {
                Transform spawnPoint = availableSpawnPoints[i];

                // --- [추가된 부분] ---
                // y축 기준으로 0도에서 360도 사이의 무작위 회전 각도를 정합니다.
                float randomYRotation = Random.Range(0f, 360f);
                // 이 회전값을 Quaternion 형태로 만듭니다.
                Quaternion spawnRotation = Quaternion.Euler(0f, randomYRotation, 0f);
                // --- [추가 끝] ---

                // 생성 시 spawnPoint.rotation 대신 방금 만든 무작위 회전값을 사용합니다.
                PhotonNetwork.Instantiate(chestPrefab.name, spawnPoint.position, spawnRotation);
            }
        }

        // 2. 아이템 목록 랜덤화
        List<string> itemNamesToShuffle = new List<string>();
        foreach (var item in itemPool)
        {
            itemNamesToShuffle.Add(item.name);
        }
        // 아이템 목록도 무작위로 섞습니다.
        for (int i = 0; i < itemNamesToShuffle.Count - 1; i++)
        {
            int rnd = Random.Range(i, itemNamesToShuffle.Count);
            string temp = itemNamesToShuffle[rnd];
            itemNamesToShuffle[rnd] = itemNamesToShuffle[i];
            itemNamesToShuffle[i] = temp;
        }

        // 섞인 아이템 목록을 모든 플레이어에게 RPC로 전송하여 동기화합니다.
        GetComponent<PhotonView>().RPC("SyncShuffledItemListRPC", RpcTarget.All, itemNamesToShuffle.ToArray());
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

    /// <summary>
    /// 상자가 열렸을 때, 방장에게 아이템을 요청하는 함수
    /// </summary>
    public void ChestOpenedByPlayer(int openerViewID)
    {
        GetComponent<PhotonView>().RPC("RequestItemFromMasterRPC", RpcTarget.MasterClient, openerViewID);
    }
}   
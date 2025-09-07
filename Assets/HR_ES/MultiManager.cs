using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class MultiManager : MonoBehaviour
{
    public GameObject santaPrefab;
    public GameObject reindeerPrefab;

    // Start is called before the first frame update
    void Start()
    {
        SpawnPlayer();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void SpawnPlayer()
    {
                string role = "Reindeer";
                if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object roleObj))
                {
                    role = roleObj as string;
                }

                GameObject prefab = role == "Santa" ? santaPrefab : reindeerPrefab;

                // 원하는 위치와 회전으로 생성 (예시: Vector3.zero)
                PhotonNetwork.Instantiate(prefab.name, new Vector3(20,5,20), Quaternion.identity);

        //기본 플레이어 생성
        //PhotonNetwork.Instantiate("PlayerArmature", Vector3.zero, Quaternion.identity);
    }
}

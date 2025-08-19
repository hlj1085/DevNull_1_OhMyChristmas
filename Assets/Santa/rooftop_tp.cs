using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rooftop_tp : MonoBehaviour
{
    public GameObject mapUI; // 지도 UI 오브젝트를 인스펙터에서 연결
    private bool isPlayerNearby = false; // 플레이어가 범위 안에 있는지 확인용

    void Start()
    {
        // 게임 시작 시 지도 UI는 꺼져 있음
        if (mapUI != null)
        {
            mapUI.SetActive(false);
        }
    }

    void Update()
    {
        // F 키를 누르고 플레이어가 근처에 있을 때 지도 UI를 표시
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F))
        {
            if (mapUI != null)
            {
                // 지도 UI on/off 토글
                bool isActive = mapUI.activeSelf;
                mapUI.SetActive(!isActive);
            }
        }
    }

    // 플레이어가 트리거 콜라이더에 들어왔을 때
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            Debug.Log("굴뚝 근처에 도착했습니다. F 키로 지도를 열 수 있습니다.");
        }
    }

    // 플레이어가 트리거 콜라이더에서 나갔을 때
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            Debug.Log("굴뚝에서 멀어졌습니다.");

            // 나가면 지도 UI 자동으로 닫기
            if (mapUI != null && mapUI.activeSelf)
            {
                mapUI.SetActive(false);
            }
        }
    }
}

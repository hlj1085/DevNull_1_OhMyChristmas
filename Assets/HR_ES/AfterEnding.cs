using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AfterEnding : MonoBehaviour
{
    // 다음 게임을 위해 방으로 돌아가는 함수
    public void GoToRoomScene()
    {
        Debug.Log("방으로 돌아갑니다.");
        SceneManager.LoadScene("RoomScene");
    }

    // 방에서 나가고 메인 화면으로 돌아가는 함수
    public void ExitToMain()
    {
        Debug.Log("게임을 나가고 메인 화면으로 돌아갑니다.");
        PhotonNetwork.LeaveRoom(); // 현재 방에서 나가기
        SceneManager.LoadScene("Server_1"); // 메인 화면(Server_1) 씬 로드
    }
}

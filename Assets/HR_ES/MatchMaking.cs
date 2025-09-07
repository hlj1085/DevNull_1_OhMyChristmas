using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public class MatchMaking : MonoBehaviourPunCallbacks, ILobbyCallbacks
{
    public TextMeshProUGUI playerListText;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        // 연결이 되면 로비에 접속
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        // 로비에 접속하면 무작위 방에 참가
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // 무작위 방 참가가 실패하면 새로운 방을 생성
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 5 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Enter Room");
        // 방에 들어가면 씬을 전환
        // UpdatePlayerList()는 PlayerRoleSelector.cs에서 처리되므로 여기서는 제거
        SceneManager.LoadScene("RoomScene");
    }

    // '게임 참가' 버튼 클릭 시 호출될 함수
    public void OnJoinRoomButton()
    {
        // 서버에 연결되지 않은 상태라면, 먼저 서버에 연결
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Connecting to Photon server...");
        }
        // 이미 연결된 상태라면, 바로 로비에 접속하여 방을 찾음
        else if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
            Debug.Log("Joining lobby...");
        }
        else
        {
            PhotonNetwork.JoinRandomRoom();
            Debug.Log("Attempting to join a random room...");
        }
    }

    public void ExitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        // 유니티 에디터에서 실행 중일 때
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 게임에서 실행 중일 때
        Application.Quit();
#endif
    }
}
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class MatchMaking : MonoBehaviourPunCallbacks
{
    // "게임 참가" 버튼을 눌렀는지 여부를 확인하는 스위치 변수
    private bool isConnecting = false;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    /// <summary>
    /// '게임 참가' 버튼 클릭 시 호출될 함수입니다.
    /// </summary>
    public void OnJoinGameButtonClick()
    {
        Debug.Log("게임 참가 버튼 클릭. 매치메이킹을 시작합니다.");
        // 연결 스위치를 켭니다.
        isConnecting = true;

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            // 이미 연결되어 있다면 바로 로비 참가를 시도합니다.
            PhotonNetwork.JoinLobby();
        }
    }

    // --- Photon Callback 함수들 ---

    public override void OnConnectedToMaster()
    {
        Debug.Log("마스터 서버 접속 성공.");
        // 연결 스위치가 켜져 있을 때만(버튼을 눌렀을 때만) 자동으로 다음 단계를 진행합니다.
        if (isConnecting)
        {
            Debug.Log("자동으로 로비에 접속합니다...");
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("로비 접속 성공.");
        // 연결 스위치가 켜져 있을 때만 자동으로 다음 단계를 진행합니다.
        if (isConnecting)
        {
            Debug.Log("자동으로 랜덤 방에 참가합니다...");
            PhotonNetwork.JoinRandomRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"랜덤 방 참가 실패: {message}. 새로운 방을 생성합니다.");
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 5 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"'{PhotonNetwork.CurrentRoom.Name}' 방에 성공적으로 참가했습니다.");
        // 방에 들어왔으니 연결 절차는 끝났습니다. 스위치를 꺼줍니다.
        isConnecting = false;

        PhotonNetwork.LoadLevel("RoomScene");
    }

    public void ExitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
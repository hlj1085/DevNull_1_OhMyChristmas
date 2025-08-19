using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// MonoBehaviourPunCallbacks를 상속받아 포톤의 주요 연결 이벤트들을 쉽게 사용할 수 있습니다.
public class PhotonConnectionManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        Debug.Log("포톤 서버에 연결을 시도합니다...");
        // 가장 기본적인 연결 방법입니다. 포톤 서버 세팅값으로 연결을 시도합니다.
        PhotonNetwork.ConnectUsingSettings();
    }

    // 포톤 마스터 서버에 접속했을 때 호출되는 콜백 함수
    public override void OnConnectedToMaster()
    {
        Debug.Log("포톤 마스터 서버에 연결 성공!");
        // 마스터 서버에 접속 후, 자동으로 로비에 접속합니다.
        PhotonNetwork.JoinLobby();
    }

    // 로비에 접속했을 때 호출되는 콜백 함수
    public override void OnJoinedLobby()
    {
        Debug.Log("로비에 성공적으로 접속했습니다.");
        // 테스트를 위해 바로 랜덤 룸에 입장을 시도합니다.
        PhotonNetwork.JoinRandomRoom();
    }

    // 랜덤 룸 입장에 실패했을 때 (방이 없을 경우 등) 호출되는 콜백 함수
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning("랜덤 룸 입장에 실패했습니다. 새로운 방을 만듭니다.");
        // 방이 없어서 실패한 것이므로, "TestRoom"이라는 이름으로 새로운 방을 만듭니다.
        PhotonNetwork.CreateRoom("TestRoom", new RoomOptions { MaxPlayers = 4 });
    }

    // 방에 성공적으로 들어갔을 때 호출되는 콜백 함수
    public override void OnJoinedRoom()
    {
        Debug.Log("'" + PhotonNetwork.CurrentRoom.Name + "' 방에 참가 성공!");
        Debug.Log("이제 RPC 테스트가 가능합니다!");
    }

    // 서버와 연결이 끊겼을 때 호출되는 콜백 함수
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarningFormat("서버와 연결이 끊겼습니다. 원인: {0}", cause);
    }
}
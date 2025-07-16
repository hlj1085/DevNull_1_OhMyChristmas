using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public class MatchMaking : MonoBehaviourPunCallbacks, ILobbyCallbacks
{
    public TextMeshProUGUI playerListText; // UI element to display the player list

    // Start is called before the first frame update
    void Start()
    {
        PhotonNetwork.ConnectUsingSettings(); //서버 연결
        // Connect to Photon server using settings defined in the PhotonServerSettings file
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void OnConnectedToMaster()
    {
        //마스터 서버에 연결되면 로비에 접속
        Debug.Log("Connected to Master Server");
        // Optionally, join a lobby or create/join a room here
        PhotonNetwork.JoinLobby(); // Join the default lobby
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 5 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Enter Room");
        UpdatePlayerList();
        SceneManager.LoadScene("RoomScene");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
    }

    void UpdatePlayerList()
    {
        if (playerListText == null) return;

        string list = "Player (" + PhotonNetwork.CurrentRoom.PlayerCount + "/5):\n";
        foreach (var player in PhotonNetwork.PlayerList)
        {
            list += player.NickName + "\n";
        }
        playerListText.text = list;
    }
    public void OnJoinRoomButton()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            Debug.LogWarning("포톤 서버에 아직 연결되지 않았습니다.");
        }
    }
}

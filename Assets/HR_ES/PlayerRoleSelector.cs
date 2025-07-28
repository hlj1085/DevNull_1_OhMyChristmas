using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRoleSelector : MonoBehaviourPunCallbacks
{
    public Button santaButton;
    public Button reindeerButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI playerListText; // UI element to display the player list

    void Start()
    {
        santaButton.onClick.AddListener(() => TrySelectRole("Santa"));
        reindeerButton.onClick.AddListener(() => TrySelectRole("Reindeer"));
        UpdateRoleButtons();
        UpdatePlayerList();
    }

    void TrySelectRole(string role)
    {
        if (role == "Santa" && !IsSantaTaken())
        {
            SetPlayerRole("Santa");
        }
        else if (role == "Reindeer" && GetReindeerCount() < 4)
        {
            SetPlayerRole("Reindeer");
        }
        else
        {
            statusText.text = "You Can't select this role";
        }
        UpdateRoleButtons();
        UpdatePlayerList();
    }

    void SetPlayerRole(string role)
    {
        var props = new ExitGames.Client.Photon.Hashtable { { "Role", role } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        statusText.text = $"{role} selected";
        UpdatePlayerList();
    }

    bool IsSantaTaken()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.TryGetValue("Role", out object roleObj) && roleObj as string == "Santa")
                return true;
        }
        return false;
    }

    int GetReindeerCount()
    {
        int count = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.TryGetValue("Role", out object roleObj) && roleObj as string == "Reindeer")
                count++;
        }
        return count;
    }
    void UpdatePlayerList()
    {
        if (playerListText == null) return;

        string list = "Player List\n";
        foreach (var player in PhotonNetwork.PlayerList)
        {
            string role = "Unselected";
            if (player.CustomProperties.TryGetValue("Role", out object roleObj) && roleObj is string roleStr)
            {
                role = roleStr;
            }
            list += $"{player.NickName} : {role}\n";
        }
        playerListText.text = list;
    }

    void UpdateRoleButtons()
    {
        object myRoleObj;
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out myRoleObj);
        string myRole = myRoleObj as string;

        santaButton.interactable = !IsSantaTaken() || myRole == "Santa";
        reindeerButton.interactable = GetReindeerCount() < 4 || myRole == "Reindeer";
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        UpdateRoleButtons();
        UpdatePlayerList();

        if (PhotonNetwork.IsMasterClient && AllRolesSelected())
        {
            Debug.Log("모든 역할 선택 완료. 게임 씬으로 이동합니다.");
            PhotonNetwork.LoadLevel("GameTest");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
    }

    bool AllRolesSelected()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount != 5)
            return false;

        int santaCount = 0;
        int reindeerCount = 0;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.TryGetValue("Role", out object roleObj) && roleObj is string roleStr)
            {
                if (roleStr == "Santa") santaCount++;
                else if (roleStr == "Reindeer") reindeerCount++;
            }
        }

        return santaCount == 1 && reindeerCount == 4;
    }
}

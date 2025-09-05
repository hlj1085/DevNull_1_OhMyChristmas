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
    // UI Canvases
    public GameObject roleSelectCanvas;
    public GameObject afterSelectCanvas;

    // Role Selection UI
    public Button santaButton;
    public Button reindeerButton;

    // After Select UI
    public Button startGameButton;
    public Transform playerCardsParent;
    public TextMeshProUGUI[] playerNicknames;
    public Image[] playerRoleIcons;
    public Image[] playerBellImages; // 각 카드 아래의 벨 이미지들

    // Role Sprites
    public Sprite santaIconSprite;
    public Sprite reindeerIconSprite;

    private const int MaxReindeers = 4;
    private const int MaxPlayers = 5;
    private const int DemoPlayerCount = 3;

    void Start()
    {
        // UI 초기 상태 설정
        roleSelectCanvas.SetActive(true);
        afterSelectCanvas.SetActive(false);
        startGameButton.gameObject.SetActive(false);

        // 버튼 리스너 등록
        santaButton.onClick.AddListener(() => TrySelectRole("Santa"));
        reindeerButton.onClick.AddListener(() => TrySelectRole("Reindeer"));
        startGameButton.onClick.AddListener(LoadGameScene);

        UpdateAllUI();
    }

    private void Update()
    {
        // 개발자용: P 키로 강제 게임 시작 (마스터 클라이언트만)
        if (PhotonNetwork.IsMasterClient && Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("개발자 강제 게임 시작(P 키)");
            PhotonNetwork.LoadLevel("Playground"); // 실제 게임 씬 이름에 맞게 수정
        }
    }

    void TrySelectRole(string role)
    {
        if (IsRoleAlreadySelected(PhotonNetwork.LocalPlayer))
        {
            Debug.Log("You have already selected a role.");
            return;
        }

        if (role == "Santa" && !IsSantaTaken())
        {
            SetPlayerRole("Santa");
            SwitchToAfterSelectUI();
        }
        else if (role == "Reindeer" && GetReindeerCount() < MaxReindeers)
        {
            SetPlayerRole("Reindeer");
            SwitchToAfterSelectUI();
        }
    }

    void SetPlayerRole(string role)
    {
        var props = new ExitGames.Client.Photon.Hashtable { { "Role", role } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    void SwitchToAfterSelectUI()
    {
        roleSelectCanvas.SetActive(false);
        afterSelectCanvas.SetActive(true);
    }

    bool IsRoleAlreadySelected(Player player)
    {
        return player.CustomProperties.ContainsKey("Role");
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

    void UpdateAllUI()
    {
        UpdateRoleButtons();
        UpdatePlayerCards();
        UpdateStartGameButton();
    }

    void UpdateRoleButtons()
    {
        santaButton.interactable = !IsSantaTaken();
        reindeerButton.interactable = GetReindeerCount() < MaxReindeers;

        // 역할이 다 찼을 때 회색으로 비활성화 (선택 불가능)
        if (!santaButton.interactable)
        {
            santaButton.GetComponent<Image>().color = Color.gray;
        }
        else
        {
            santaButton.GetComponent<Image>().color = Color.white;
        }

        if (!reindeerButton.interactable)
        {
            reindeerButton.GetComponent<Image>().color = Color.gray;
        }
        else
        {
            reindeerButton.GetComponent<Image>().color = Color.white;
        }
    }

    void UpdatePlayerCards()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            // 플레이어가 있을 경우
            if (i < PhotonNetwork.PlayerList.Length)
            {
                var player = PhotonNetwork.PlayerList[i];
                string nickname = $"Player {i + 1}";
                string role = "Unselected";
                Sprite roleSprite = null; // 초기 스프라이트 없음

                if (player.CustomProperties.TryGetValue("Role", out object roleObj) && roleObj is string roleStr)
                {
                    role = roleStr;
                    roleSprite = (roleStr == "Santa") ? santaIconSprite : reindeerIconSprite;
                }

                playerNicknames[i].text = nickname;
                playerRoleIcons[i].sprite = roleSprite;
                playerRoleIcons[i].color = (role == "Unselected") ? Color.clear : Color.white;
                playerBellImages[i].gameObject.SetActive(true); // 벨 이미지 활성화
            }
            // 플레이어가 없을 경우
            else
            {
                playerNicknames[i].text = "Waiting...";
                playerRoleIcons[i].sprite = null; // 이미지 비우기
                playerRoleIcons[i].color = Color.clear; // 완전히 투명하게 설정
                playerBellImages[i].gameObject.SetActive(false); // 벨 이미지 비활성화
            }
        }
    }

    void UpdateStartGameButton()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            startGameButton.gameObject.SetActive(false);
            return;
        }

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

        // 시연용: 산타 1명, 순록 2명일 때 버튼 활성화
        if (santaCount == 1 && reindeerCount == 2)
        {
            startGameButton.gameObject.SetActive(true);
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
        }
    }

    void LoadGameScene()
    {
        // 마스터 클라이언트만 씬 로드
        if (PhotonNetwork.IsMasterClient)
        {
            if (AreAllRolesSelectedForStart())
            {
                Debug.Log("게임 시작! 씬으로 이동합니다.");
                PhotonNetwork.LoadLevel("Playground");
            }
        }
    }

    bool AreAllRolesSelectedForStart()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount != DemoPlayerCount)
        {
            return false;
        }

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

        return santaCount == 1 && reindeerCount == 2;
    }

    // Photon Callbacks
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        UpdateAllUI();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateAllUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateAllUI();
    }
}
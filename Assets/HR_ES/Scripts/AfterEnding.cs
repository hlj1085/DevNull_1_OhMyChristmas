using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// 이 스크립트가 RPC를 사용하려면 PhotonView가 필요합니다.
[RequireComponent(typeof(PhotonView))]
public class AfterEnding : MonoBehaviourPunCallbacks
{
    [Header("버튼 오브젝트")]
    [Tooltip("계속하기 버튼은 방장에게만 보입니다.")]
    public GameObject continueButton;
    public GameObject exitButton; // 나가기 버튼 변수 추가

    [Header("엔딩 영상")]
    [Tooltip("영상을 재생할 VideoPlayer 컴포넌트")]
    public VideoPlayer videoPlayer;
    [Tooltip("영상이 출력될 RawImage UI")]
    public RawImage videoDisplay;

    [Header("영상 클립")]
    public VideoClip reindeerWinClip;
    public VideoClip santaWinClip;
    public VideoClip drawClip;

    private void Start()
    {
        // 버튼들을 우선 비활성화합니다.
        if (continueButton != null) continueButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(false);

        // 비디오 플레이어의 '재생 완료' 이벤트를 구독합니다.
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
        }
        /// <summary>
        /// 비디오 재생이 끝나면 자동으로 호출될 함수입니다.
        /// </summary>
        void OnVideoFinished(VideoPlayer vp)
        {
            Debug.Log("영상 재생 완료. 비디오를 끄고 버튼을 표시합니다.");

            // 영상 화면을 비활성화합니다.
            if (videoDisplay != null)
            {
                videoDisplay.gameObject.SetActive(false);
            }

            // 버튼들을 활성화합니다.
            // 방장일 경우에만 '계속하기' 버튼을 활성화합니다.
            if (continueButton != null)
            {
                continueButton.SetActive(PhotonNetwork.IsMasterClient);
            }
            if (exitButton != null)
            {
                exitButton.SetActive(true);
            }
        }


        // 방장일 경우에만 '계속하기' 버튼을 활성화합니다.
        if (continueButton != null)
        {
            continueButton.SetActive(PhotonNetwork.IsMasterClient);
        }

        // 마우스 커서 잠금 해제 및 보이기 (안전을 위해 여기서도 한 번 더 호출)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// '나가기' 버튼에 연결할 함수입니다.
    /// </summary>
    public void GoToLobbyScene()
    {
        Debug.Log("게임을 나가고 로비 화면으로 돌아갑니다. 방을 떠납니다...");
        PhotonNetwork.LeaveRoom();
    }

    /// <summary>
    /// LeaveRoom()이 성공적으로 완료되었을 때 포톤이 자동으로 호출해주는 콜백 함수입니다.
    /// </summary>
    public override void OnLeftRoom()
    {
        Debug.Log("방에서 성공적으로 나갔습니다. 로비 씬을 로드합니다.");
        // "RoleSelectScene" 부분은 실제 역할 선택 씬 이름으로 변경해주세요.
        PhotonNetwork.LoadLevel("Server_1");
    }

    // --- [이 아래 함수들이 추가되었습니다] ---

    /// <summary>
    /// '계속하기' 버튼에 연결할 함수입니다. 방장만 호출할 수 있습니다.
    /// </summary>
    public void ContinueGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("방장이 '계속하기'를 눌렀습니다. 모든 플레이어를 역할 선택 씬으로 이동시킵니다.");
            // 이 스크립트가 붙은 게임오브젝트의 PhotonView를 사용해 RPC를 호출합니다.
            photonView.RPC("GoToRoleSelectSceneRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void GoToRoleSelectSceneRPC()
    {
        Debug.Log("역할 선택 씬으로 이동하라는 RPC를 수신했습니다.");
        // "RoleSelectScene" 부분은 실제 역할 선택 씬 이름으로 변경해주세요.
        PhotonNetwork.LoadLevel("RoomScene");
    }
    /// <summary>
    /// 게임 결과에 맞는 엔딩 비디오를 재생합니다.
    /// </summary>
    public void PlayEndingVideo(GameResult result)
    {
        // 비디오 플레이어가 없으면 아무것도 하지 않음
        if (videoPlayer == null) return;

        // 영상 출력을 위해 RawImage를 활성화
        if (videoDisplay != null) videoDisplay.gameObject.SetActive(true);

        // switch 문을 사용하여 결과에 따라 다른 클립을 할당
        switch (result)
        {
            case GameResult.ReindeerWin:
                videoPlayer.clip = reindeerWinClip;
                break;
            case GameResult.SantaWin:
                videoPlayer.clip = santaWinClip;
                break;
            case GameResult.Draw:
                videoPlayer.clip = drawClip;
                break;
        }

        // 선택된 클립을 재생
        videoPlayer.Play();
    }
}
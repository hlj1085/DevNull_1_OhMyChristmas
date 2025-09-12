using Photon.Pun;
using UnityEngine;
using TMPro;
public enum GameResult { ReindeerWin, SantaWin, Draw }

public class FairyManager : MonoBehaviourPunCallbacks
{
    public static FairyManager instance;

    [Header("엔딩 설정")]
    [Tooltip("게임 종료 시 활성화될 오브젝트 (예: '탈출 성공!' UI)")]
    public GameObject afterEndingObject;

    [Tooltip("게임 종료 시 켜질 전체 조망용 카메라")]
    public Camera endingCamera;

    [Header("요정가루 설정")]
    public int totalFairyDust = 0;         // 전체 요정가루 개수
    public int requiredDust = 10;          // 탈출구를 열기 위해 필요한 개수
    public GameObject escapePortal;        // 탈출구 오브젝트 (기본은 비활성화)
                                           // FairyManager.cs 스크립트 상단 변수 선언 부분에 추가해주세요.

    [Header("UI 참조")]
    [Tooltip("산타의 인게임 UI 캔버스 오브젝트")]
    public GameObject santaGameUI;
    [Tooltip("순록의 인게임 UI 캔버스 오브젝트")]
    public GameObject reindeerGameUI;

    [Header("게임 타이머")]
    [Tooltip("게임 제한 시간 (초 단위)")]
    public float gameTimeLimit = 600f; // 10분
    [Tooltip("순록 승리 시간 기준 (초 단위)")]
    public float reindeerWinTime = 300f; // 5분
    [Tooltip("타이머를 표시할 UI 텍스트")]
    public TextMeshProUGUI timerText;
    private float currentTime = 0f;
    private bool isGameEnded = false;

    // --- Update() 함수를 추가하거나, 기존 함수가 있다면 내용을 수정해주세요 ---
    void Update()
    {
        // 타이머 UI가 연결되어 있을 때만 실행
        if (timerText != null)
        {
            // 남은 시간을 계산 (제한 시간 - 현재 시간)
            float remainingTime = gameTimeLimit - currentTime;
            // 0초 이하로 내려가지 않도록 보정
            remainingTime = Mathf.Max(0, remainingTime);

            // 분과 초로 변환
            int minutes = (int)(remainingTime / 60);
            int seconds = (int)(remainingTime % 60);
            // "05:03" 과 같은 형식으로 텍스트를 업데이트
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        // 방장(Master Client)만 시간을 계산하고, 게임 종료를 판정합니다.
        if (PhotonNetwork.IsMasterClient)
        {
            // 아직 게임이 끝나지 않았다면 시간 증가
            if (!isGameEnded)
            {
                currentTime += Time.deltaTime;

                // 시간이 다 되었다면 산타 승리로 게임 종료
                if (currentTime >= gameTimeLimit)
                {
                    Debug.Log("시간 초과! 산타 승리.");
                    TriggerGameEnd(GameResult.SantaWin);
                }
            }
        }
    }

    // --- OnPhotonSerializeView() 함수를 추가하여 시간을 동기화합니다 ---
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 방장은 현재 시간을 다른 클라이언트에게 보냅니다.
            stream.SendNext(currentTime);
        }
        else
        {
            // 다른 클라이언트들은 방장으로부터 현재 시간을 받습니다.
            this.currentTime = (float)stream.ReceiveNext();
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    [PunRPC]
    public void AddFairyDust(int amount)
    {
        totalFairyDust += amount;
        Debug.Log($"[FairyManager] 요정가루 추가됨. 현재 총합: {totalFairyDust}");

        // UI 갱신 (모든 산타/순록 화면에 표시)
        UIManager.instance.UpdateFairyDustUI(totalFairyDust);

        // 탈출구 열 조건 체크
        if (totalFairyDust >= requiredDust)
        {
            OpenEscapePortal();
        }
    }

    private void OpenEscapePortal()
    {
        if (escapePortal != null)
        {
            escapePortal.SetActive(false);
            Debug.Log("[FairyManager] 탈출구가 열렸습니다!");
        }
    }

    // 다른 클라이언트와 동기화
    public void AddDustNetworked(int amount)
    {
        photonView.RPC("AddFairyDust", RpcTarget.All, amount);
    }
    // FairyManager.cs 파일에 아래 내용을 추가하세요.



    /// <summary>
    /// 순록이 탈출했을 때 호출되는 함수입니다.
    /// </summary>
    public void ReindeerEscaped()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("순록 탈출! 시간을 기준으로 승패를 판정합니다.");
            if (currentTime < reindeerWinTime) // 5분 미만
            {
                TriggerGameEnd(GameResult.ReindeerWin);
            }
            else if (currentTime < gameTimeLimit) // 5분 ~ 10분 사이
            {
                TriggerGameEnd(GameResult.Draw);
            }
            else // 10분 이후 (시간 초과와 동일)
            {
                TriggerGameEnd(GameResult.SantaWin);
            }
        }
    }

    /// <summary>
    /// 게임 종료 로직을 시작시키는 함수.
    /// </summary>
    public void TriggerGameEnd(GameResult result)
    {
        if (!isGameEnded)
        {
            isGameEnded = true;
            // 모든 플레이어에게 게임 결과와 함께 게임 종료를 알립니다.
            photonView.RPC("EndGameRPC", RpcTarget.All, (int)result);
        }
    }
    [PunRPC]
    private void EndGameRPC( int resultValue)
    {
        GameResult result = (GameResult)resultValue; // 다시 enum으로 변환
        Debug.Log($"EndGameRPC 수신. 결과: {result}. 엔딩 시퀀스를 시작합니다.");

        if (santaGameUI != null) santaGameUI.SetActive(false);
        if (reindeerGameUI != null) reindeerGameUI.SetActive(false);

        // 잠겨있던 마우스 커서를 풀어주고, 다시 보이게 만듭니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 1. AfterEnding 오브젝트 활성화
        if (afterEndingObject != null)
        {
            afterEndingObject.SetActive(true);
            afterEndingObject.GetComponent<AfterEnding>()?.PlayEndingVideo(result);
        }

        // 씬에 있는 모든 순록 컨트롤러를 찾습니다.
        ReindeerController[] allReindeers = FindObjectsOfType<ReindeerController>();
        foreach (var reindeer in allReindeers)
        {
            // 각 순록의 카메라와 조작을 비활성화합니다.
            reindeer.DisableForEnding();
        }

        // (필요하다면 산타도 비활성화)
        // SantaController santa = FindObjectOfType<SantaController>();
        // if (santa != null) santa.DisableForEnding();

        // 2. 엔딩 카메라 활성화
        if (endingCamera != null)
        {
            endingCamera.gameObject.SetActive(true);
        }
    }
}

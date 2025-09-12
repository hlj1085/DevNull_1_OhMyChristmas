using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

// 씬에 단 하나만 존재하도록 강제하고, 오디오 소스를 필수로 요구합니다.
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PhotonView))]
public class SoundManager : MonoBehaviour
{
    // 싱글톤 인스턴스: 어디서든 SoundManager.instance 로 쉽게 접근 가능
    public static SoundManager instance;

    [Header("사운드 클립")]
    [Tooltip("여기에 모든 효과음 오디오 클립을 넣으세요.")]
    public AudioClip[] audioClips;

    private AudioSource audioSource;
    private PhotonView photonView;
    // 오디오 클립을 이름으로 쉽게 찾기 위한 딕셔너리(사전)
    private Dictionary<string, AudioClip> audioClipDict = new Dictionary<string, AudioClip>();

    void Awake()
    {
        // 싱글톤 패턴 설정
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        photonView = GetComponent<PhotonView>();

        // 배열에 있는 오디오 클립들을 딕셔너리에 이름으로 저장
        foreach (var clip in audioClips)
        {
            if (clip != null)
            {
                audioClipDict[clip.name] = clip;
            }
        }
    }

    /// <summary>
    /// 로컬에서만 사운드를 재생합니다. (UI 효과음 등)
    /// </summary>
    public void PlaySound(string clipName)
    {
        if (audioClipDict.TryGetValue(clipName, out AudioClip clip))
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"SoundManager: '{clipName}' 사운드를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 네트워크상의 모든 플레이어에게 사운드를 재생합니다. (인게임 효과음 등)
    /// </summary>
    public void PlaySoundNetworked(string clipName)
    {
        photonView.RPC("PlaySoundRPC", RpcTarget.All, clipName);
    }

    [PunRPC]
    private void PlaySoundRPC(string clipName)
    {
        if (audioClipDict.TryGetValue(clipName, out AudioClip clip))
        {
            // 3D 사운드가 아닌 2D 사운드로 재생하기 위해 PlayClipAtPoint 대신 PlayOneShot 사용
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"SoundManager: '{clipName}' 사운드를 찾을 수 없습니다.");
        }
    }
    void TrySelectRole(string role)
    {
        SoundManager.instance.PlaySound("효과음 - ui 선택"); // UI 사운드는 로컬에서만
                                                        // ... 기존 코드
    }
    public bool Interact(GameObject interactorObject)
    {
        // ... 기존 코드
        if (interactorObject != null)
        {
            SoundManager.instance.PlaySoundNetworked("효과음 - 상자 열기 효과음"); // 모든 사람이 듣도록                                                                // ... 기존 코드
        }

        return false;
    }
}
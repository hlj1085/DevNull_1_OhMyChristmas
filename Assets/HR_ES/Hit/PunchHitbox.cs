using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PunchHitbox : MonoBehaviour
{
    public SantaFirstPersonController santa; // 상위 컨트롤러
    public int damage = 1;
    public float hitCooldownMs = 150f; // 한 스윙 내 중복 방지
    public LayerMask hurtboxLayer;     // Hurtbox 레이어

    Collider col;
    PhotonView pv;
    bool active;
    int swingSeq;                      // 스윙 시퀀스
    double lastOpenTime;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        pv = santa.GetComponent<PhotonView>();
        DeactivateWindow();
    }

    public void ActivateWindow()
    {
        active = true;
        swingSeq++;
        lastOpenTime = PhotonNetwork.Time;
    }

    public void DeactivateWindow()
    {
        active = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!active) return;
        if (!pv.IsMine) return; // 내 산타만 히트 시도

        var hurt = other.GetComponent<Hurtbox>();
        if (hurt == null) return;

        var targetView = hurt.OwnerView;
        if (targetView == null) return;

        // 로컬 예측 FX
        PlayLocalHitFX(other.ClosestPoint(transform.position));

        // 히트 시도 패킷 전송(마스터만 수신)
        var data = new object[] {
            pv.ViewID,                          // attacker
            targetView.ViewID,                  // victim
            damage,
            other.ClosestPoint(transform.position),
            PhotonNetwork.Time,                 // 시도 시각
            swingSeq                            // 시퀀스(중복 방지)
        };

        PhotonNetwork.RaiseEvent(
            NetEv.HitAttempt,
            data,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable
        );

        // 한 번 맞췄으면 당장 창 닫아 중복 감소
        active = false;
    }

    void PlayLocalHitFX(Vector3 pos)
    {
        // ParticleSystem.Play(), AudioSource.PlayOneShot()
    }
}

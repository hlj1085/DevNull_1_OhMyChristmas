using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class HitMaster : MonoBehaviourPunCallbacks
{
    public float maxHitDistance = 3.0f; // 무기/주먹 사거리
    public LayerMask lineOfSightMask;   // 장애물 레이어

    void OnEnable() => PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    void OnDisable() => PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;

    void OnEvent(EventData ev)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (ev.Code != NetEv.HitAttempt) return;

        var a = (object[])ev.CustomData;
        int attackerId = (int)a[0];
        int victimId = (int)a[1];
        int damage = (int)a[2];
        Vector3 contact = (Vector3)a[3];
        double sentTime = (double)a[4];
        int swingSeq = (int)a[5];

        var attacker = PhotonView.Find(attackerId);
        var victim = PhotonView.Find(victimId);
        if (attacker == null || victim == null) return;

        if (!IsValidHit(attacker.transform, victim.transform, contact, sentTime))
            return;

        var confirm = new object[] {
            attackerId, victimId, damage, contact, PhotonNetwork.Time, swingSeq
        };

        PhotonNetwork.RaiseEvent(
            NetEv.HitConfirmed,
            confirm,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable
        );
    }

    bool IsValidHit(Transform attacker, Transform victim, Vector3 contact, double sentTime)
    {
        // 현재 위치 기준 거리/시야 체크
        float dist = Vector3.Distance(attacker.position, victim.position);
        if (dist > maxHitDistance) return false;

        // 시야(장애물) 체크
        Vector3 origin = attacker.position + Vector3.up * 1.5f;
        if (Physics.Linecast(origin, contact, out var hit, lineOfSightMask))
        {
            return false;
        }

        return true;
    }
}

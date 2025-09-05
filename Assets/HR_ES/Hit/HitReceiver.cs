using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class HitReceiver : MonoBehaviourPunCallbacks
{
    PhotonView pv;
    ReindeerController deer;

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        deer = GetComponent<ReindeerController>();
    }

    void OnEnable() => PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    void OnDisable() => PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;

    void OnEvent(EventData ev)
    {
        if (ev.Code != NetEv.HitConfirmed) return;

        var d = (object[])ev.CustomData;
        int attackerId = (int)d[0];
        int victimId = (int)d[1];
        int damage = (int)d[2];
        Vector3 contact = (Vector3)d[3];
        double authTime = (double)d[4];
        int swingSeq = (int)d[5];

        // 내가 피해자라면 상태 적용
        if (pv != null && pv.ViewID == victimId)
        {
            deer?.GetStunned();
            PlayHitFX(contact);
        }
    }

    void PlayHitFX(Vector3 pos)
    {
    }
}

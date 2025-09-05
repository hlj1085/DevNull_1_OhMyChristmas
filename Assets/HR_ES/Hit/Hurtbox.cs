using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Hurtbox : MonoBehaviour
{
    public ReindeerController owner; // 순록의 최상위 컨트롤러
    public PhotonView OwnerView => owner ? owner.GetComponent<PhotonView>() : null;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // 반드시 Trigger
    }
}

public static class NetEv
{
    public const byte HitAttempt = 1; // 공격자 -> 마스터
    public const byte HitConfirmed = 2; // 마스터 -> 모두
}

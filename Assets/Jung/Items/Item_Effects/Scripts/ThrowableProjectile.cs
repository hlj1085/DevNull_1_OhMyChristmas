using UnityEngine;
using Photon.Pun;

// IPunInstantiateMagicCallback 인터페이스를 추가
public class ThrowableProjectile : MonoBehaviour, IPunInstantiateMagicCallback
{
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 이 함수는 PhotonNetwork.Instantiate로 생성되는 즉시 자동으로 호출됩니다.
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // 1. 함께 전달된 '초기 속도' 데이터를 받습니다.
        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0)
        {
            Vector3 initialVelocity = (Vector3)data[0];

            // 2. 받은 속도를 자신의 Rigidbody에 즉시 적용합니다.
            if (rb != null)
            {
                rb.velocity = initialVelocity;
            }
        }

        // 5초 뒤에 자동으로 파괴 (네트워크 오브젝트는 PhotonNetwork.Destroy 사용)
        if (info.photonView.IsMine)
        {
            StartCoroutine(DestroyAfterTime(5f));
        }
    }

    private System.Collections.IEnumerator DestroyAfterTime(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.Destroy(this.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 충돌 후 파괴는 마스터 클라이언트만 결정하도록 하여 동기화 오류를 막습니다.
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
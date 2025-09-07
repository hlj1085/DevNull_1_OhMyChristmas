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
        if (PhotonNetwork.IsMasterClient)
        {
            // 'Santa' 태그 대신 'SantaController' 스크립트로 확인하는 것이 더 안정적
            SantaController santa = collision.gameObject.GetComponent<SantaController>();
            if (santa != null)
            {
                Debug.Log("산타를 맞췄다!");
                PhotonView santaPhotonView = santa.GetComponent<PhotonView>();
                if (santaPhotonView != null)
                {
                    // 넉백 방향과 힘을 RPC로 전달
                    Vector3 knockbackDir = (santa.transform.position - transform.position).normalized;
                    santaPhotonView.RPC("ApplyKnockback", RpcTarget.All, knockbackDir, 10f); // 10f는 넉백 힘
                }
            }
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
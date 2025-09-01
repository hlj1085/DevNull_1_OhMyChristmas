using UnityEngine;
using Photon.Pun;

public class ThrowableProjectile : MonoBehaviour
{
    void Start()
    {
        // 5초 뒤에 자동으로 파괴
        Destroy(gameObject, 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 마스터 클라이언트에서만 충돌 로직을 처리하여 중복 방지
        if (PhotonNetwork.IsMasterClient)
        {
            // 'Santa' 태그를 가진 오브젝트와 부딪히면
            if (collision.gameObject.CompareTag("Santa"))
            {
                Debug.Log("산타를 맞췄다!");
                // (선택사항) 산타에게 데미지를 주거나 상태를 바꾸는 RPC 호출
            }
            // 부딪히면 바로 파괴
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
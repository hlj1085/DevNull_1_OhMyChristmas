using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class Chest : MonoBehaviour, IInteractable
{
    [Header("상자 구성요소")]
    [Tooltip("상자의 뚜껑 부분에 해당하는 게임 오브젝트")]
    public GameObject chestLid; // <<< 뚜껑 오브젝트를 연결할 변수 추가
    public Transform itemSpawnPoint; // <<< 'Head' 위치에 해당하는 Transform 추가

    private bool isOpened = false;
    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    public InteractionType InteractionType => InteractionType.Hold;

    public bool CanInteract(GameObject interactor)
    {
        return !isOpened && interactor.CompareTag("Reindeer");
    }

    public string GetInteractMessage(GameObject interactorObject)
    {
        return "F - 상자 열기";
    }


    public bool Interact(GameObject interactorObject)
    {
        if (isOpened) return false;

        // 상자가 열렸다고 모두에게 알리고,
        photonView.RPC("MarkAsOpenedRPC", RpcTarget.AllBuffered);

        // 방장에게 "내 위치에 아이템 하나 생성해줘!" 라고 '요청'만 합니다.
        ItemSpawnManager.instance.GetComponent<PhotonView>().RPC("RequestItemSpawnRPC", RpcTarget.MasterClient, this.photonView.ViewID);

        return true;
    }



    [PunRPC]
    private void MarkAsOpenedRPC()
    {
        isOpened = true;

        // --- [이 부분이 수정되었습니다] ---
        // 뚜껑 오브젝트가 연결되어 있다면, 비활성화하여 열린 것처럼 보이게 합니다.
        if (chestLid != null)
        {
            chestLid.SetActive(false);
        }
        // --- [수정 끝] ---

        // 효과음 재생 (모든 클라이언트가 각자 로컬에서 재생)
        SoundManager.instance.PlaySound("효과음 - 상자 열기 효과음");
    }
}
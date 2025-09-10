using UnityEngine;
using Photon.Pun; // Photon 기능을 사용하기 위해 추가

// IInteractable 외에 IPunObservable 인터페이스를 추가하여 상태 동기화
public class InteractableBox : MonoBehaviour, IInteractable, IPunObservable
{
    [Header("설정")]
    public Animator boxAnimator;
    public GameObject itemPrefabToSpawn; // 반드시 Resources 폴더 안에 있어야 함
    public Transform itemSpawnPoint;
    public GameObject boxLid;

    private bool isOpened = false;
    private PhotonView photonView; // PhotonView 참조를 위한 변수 추가

    // --- IInteractable 인터페이스 구현 ---
    public bool CanInteract(GameObject interactor)
    {
        return !isOpened;
    }
    public InteractionType InteractionType => InteractionType.Hold;

    void Awake()
    {
        // PhotonView 컴포넌트를 찾아서 저장
        photonView = GetComponent<PhotonView>();
    }

    // Interact 함수의 반환 타입을 bool로 변경하고, true를 return
    public bool Interact(GameObject interactorObject)
    {
        if (isOpened) return false; // 이미 열렸으면 상호작용 실패

        photonView.RPC("OpenBoxRPC", RpcTarget.All);
        return true; // 상호작용 시작에 성공했으므로 true 반환
    }

    [PunRPC] // 이 함수는 RPC 신호를 받은 모든 클라이언트에서 실행됩니다.
    private void OpenBoxRPC()
    {
        // 중복 실행 방지
        if (isOpened) return;

        isOpened = true;
        Debug.Log("상호작용 성공! 모든 클라이언트에서 상자를 엽니다.");

        if (boxAnimator != null)
            boxAnimator.SetTrigger("Open");

        if (boxLid != null)
            boxLid.SetActive(false);

        // --- [수정] --- 아이템 생성은 마스터 클라이언트만 담당하여 중복 생성을 방지
        if (PhotonNetwork.IsMasterClient && itemPrefabToSpawn != null)
        {
            Transform spawnPoint = itemSpawnPoint != null ? itemSpawnPoint : transform;
            string prefabPath = "Items/" + itemPrefabToSpawn.name;
            PhotonNetwork.Instantiate(prefabPath, spawnPoint.position, Quaternion.identity);
        }
    }

    // Update 함수는 아이템이 사라졌는지 체크하는 로직이므로 그대로 두어도 괜찮으나,
    // 멀티플레이에서는 아이템을 추적하는 방식이 달라져야 하므로 일단 주석 처리하는 것을 권장합니다.
    /*
    void Update()
    {
        if (isOpened && spawnedItem == null && !isFadingOut)
        {
            isFadingOut = true;
            Destroy(gameObject, 1f);
        }
    }
    */

    public string GetInteractMessage(GameObject interactorObject)
    {
        return isOpened ? "" : "Hold [F] to Open";
    }

    // --- [추가] --- 나중에 접속한 플레이어를 위한 상태 동기화
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 나의 isOpened 상태를 다른 사람에게 보냄
            stream.SendNext(isOpened);
        }
        else
        {
            // 다른 사람의 isOpened 상태를 받음
            this.isOpened = (bool)stream.ReceiveNext();

            // 만약 받은 상태가 '열림' 상태라면, 내 화면에서도 열린 모습으로 즉시 갱신
            if (this.isOpened)
            {
                if (boxAnimator != null) boxAnimator.SetTrigger("Open");
                if (boxLid != null) boxLid.SetActive(false);
            }
        }
    }
}
using UnityEngine;

public class InteractableBox : MonoBehaviour, IInteractable
{
    [Header("설정")]
    public Animator boxAnimator;
    public GameObject itemPrefabToSpawn;
    public Transform itemSpawnPoint;
    public GameObject boxLid; // 상자 뚜껑

    private bool isOpened = false;
    private ItemObject spawnedItem;
    private bool isFadingOut = false;

    public bool CanInteract => !isOpened;
    public InteractionType InteractionType => InteractionType.Hold;

    public void Interact(Inventory inventory)
    {
        if (isOpened) return;

        isOpened = true;
        Debug.Log("상호작용 성공! 상자를 엽니다.");

        if (boxAnimator != null)
            boxAnimator.SetTrigger("Open");

        if (boxLid != null)
            boxLid.SetActive(false);

        if (itemPrefabToSpawn != null)
        {
            Transform spawnPoint = itemSpawnPoint != null ? itemSpawnPoint : transform;
            GameObject itemInstance = Instantiate(itemPrefabToSpawn, spawnPoint.position, Quaternion.identity);
            spawnedItem = itemInstance.GetComponent<ItemObject>();
        }
    }

    void Update()
    {
        if (isOpened && spawnedItem == null && !isFadingOut)
        {
            isFadingOut = true;
            Destroy(gameObject, 1f);
        }
    }

    public string GetInteractMessage()
    {
        return (isOpened || isFadingOut) ? "" : "Hold [F] to Open";
    }
}
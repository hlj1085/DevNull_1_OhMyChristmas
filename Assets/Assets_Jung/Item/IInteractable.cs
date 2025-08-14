public enum InteractionType
{
    Instant, // 한 번 누르기
    Hold     // 꾹 누르기
}

public interface IInteractable
{
    // [추가] 이 오브젝트와 현재 상호작용이 가능한지 여부를 반환합니다.
    bool CanInteract { get; }

    InteractionType InteractionType { get; }
    void Interact(Inventory inventory);
    string GetInteractMessage();
}
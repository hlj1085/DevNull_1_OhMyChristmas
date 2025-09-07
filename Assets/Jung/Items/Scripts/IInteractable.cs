using UnityEngine;

public enum InteractionType
{
    Instant, // 한 번 누르기
    Hold     // 꾹 누르기
}

public interface IInteractable
{
    InteractionType InteractionType { get; }
    bool CanInteract { get; }
    string GetInteractMessage(GameObject interactorObject);


    // void에서 bool로 변경
    bool Interact(GameObject interactorObject);
}
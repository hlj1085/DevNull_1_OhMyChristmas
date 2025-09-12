using UnityEngine;

public enum InteractionType
{
    Instant, // 한 번 누르기
    Hold     // 꾹 누르기
}

public interface IInteractable
{
    InteractionType InteractionType { get; }
    bool CanInteract(GameObject interactor);
    string GetInteractMessage(GameObject interactorObject);
    bool Interact(GameObject interactorObject);
}
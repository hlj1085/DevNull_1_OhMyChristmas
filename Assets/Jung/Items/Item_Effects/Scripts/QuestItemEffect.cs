using UnityEngine;

[CreateAssetMenu(fileName = "New Quest Item Effect", menuName = "Inventory/Item Effects/Quest")]
public class QuestItemEffect : ItemEffect
{
    // 이 아이템은 사용 효과가 없으므로, ExecuteEffect 함수를 비워둡니다.
    public override void ExecuteEffect(ReindeerController user)
    {
        Debug.Log("이 아이템은 사용할 수 없습니다.");
        // 아무 일도 일어나지 않음
    }
}
using UnityEngine;

// 모든 아이템 효과 스크립트가 상속받을 설계도입니다.
// ScriptableObject를 상속받아 이 '효과' 자체도 데이터 파일로 만들 수 있게 합니다.
public abstract class ItemEffect : ScriptableObject
{
    // 자식 클래스들이 반드시 구현해야 할 '사용' 효과 함수
    public abstract void ExecuteEffect(ReindeerController user);
}
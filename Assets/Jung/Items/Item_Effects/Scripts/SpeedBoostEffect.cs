using UnityEngine;

[CreateAssetMenu(fileName = "New Speed Boost Effect", menuName = "Inventory/Item Effects/Speed Boost")]
public class SpeedBoostEffect : ItemEffect
{
    public float speedBoostAmount = 5f;
    public float boostDuration = 10f;

    public override void ExecuteEffect(ReindeerController user)
    {
        // ReindeerController에게 "이 효과를 적용해줘!" 라고 메시지를 보냅니다.
        user.ApplySpeedBoost(speedBoostAmount, boostDuration);
    }
}
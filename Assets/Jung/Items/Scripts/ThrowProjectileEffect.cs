using UnityEngine;

[CreateAssetMenu(fileName = "New Throw Projectile Effect", menuName = "Inventory/Item Effects/Throw Projectile")]
public class ThrowProjectileEffect : ItemEffect
{
    public float throwForce = 20f;
    public GameObject projectilePrefab; // Resources 폴더에 있어야 함

    public override void ExecuteEffect(ReindeerController user)
    {
        // ReindeerController에게 "이 아이템을 던져줘!" 라고 메시지를 보냅니다.
        user.ThrowItem(projectilePrefab.name, throwForce);
    }
}
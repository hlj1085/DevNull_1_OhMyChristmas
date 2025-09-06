using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public int collectedDustCount = 0;
    public int totalDustCount = 4;
    public TextMeshProUGUI dustCountText;
    public GameObject door;
    public Vector3 openAxis = new Vector3(0, 1, 0);
    public float openAngle = 90f;
    private Quaternion closedRotation;

    void Start()
    {
        if (door != null)
        {
            closedRotation = door.transform.rotation;
        }
        UpdateDustCountUI();
    }

    // 요정으로부터 요정가루를 전달받는 함수
    public void ReceiveDust()
    {
        collectedDustCount++;
        Debug.Log("요정에게 요정가루를 전달했습니다. 현재 개수: " + collectedDustCount);
        UpdateDustCountUI();

        if (collectedDustCount >= totalDustCount)
        {
            OpenDoorInstantly();
        }
    }

    private void UpdateDustCountUI()
    {
        if (dustCountText != null)
        {
            dustCountText.text = "요정가루: " + collectedDustCount + " / " + totalDustCount;
        }
    }

    private void OpenDoorInstantly()
    {
        if (door != null)
        {
            Debug.Log("모든 요정가루를 모았습니다! 문이 즉시 열립니다.");
            door.transform.rotation = closedRotation * Quaternion.Euler(openAxis.normalized * openAngle);
        }
    }
}
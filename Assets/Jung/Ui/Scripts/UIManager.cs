// UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("역할별 UI 캔버스")]
    [Tooltip("산타에게만 보일 UI 요소들의 부모 오브젝트")]
    public GameObject santaCanvas;
    [Tooltip("순록에게만 보일 UI 요소들의 부모 오브젝트")]
    public GameObject reindeerCanvas;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 플레이어 역할에 맞는 UI를 활성화하고, 필요한 컴포넌트 참조를 설정합니다.
    /// </summary>
    /// <param name="role">"Santa" 또는 "Reindeer"</param>
    public void InitializeUIForRole(string role)
    {
        if (role == "Santa")
        {
            if (santaCanvas != null) santaCanvas.SetActive(true);
            if (reindeerCanvas != null) reindeerCanvas.SetActive(false);
        }
        else // Reindeer
        {
            if (santaCanvas != null) santaCanvas.SetActive(false);
            if (reindeerCanvas != null) reindeerCanvas.SetActive(true);
        }
    }
    public void SetInventory(Inventory targetInventory, InventoryUI uiToUpdate)
    {
        if (uiToUpdate != null)
        {
            uiToUpdate.inventory = targetInventory;
        }
    }
    public void UpdateFairyDustUI(int totalDust)
    {
        string dustAmount = totalDust.ToString();

        // 1. 산타 UI 갱신
        if (santaCanvas != null)
        {
            // '?'를 사용하여 santaCanvas가 활성화 상태가 아니더라도 오류 없이 안전하게 실행됩니다.
            var fairyDustText = santaCanvas.transform.Find("Game_Status_UI_Group/Icon_FairyDust/HowmanyDust")
                                    ?.GetComponent<TMPro.TMP_Text>();

            if (fairyDustText != null)
            {
                fairyDustText.text = dustAmount;
                Debug.Log("[UIManager] 산타 UI 갱신: " + dustAmount);
            }
        }

        // 2. 순록 UI 갱신 (이 부분이 추가되었습니다)
        if (reindeerCanvas != null)
        {
            var fairyDustText = reindeerCanvas.transform.Find("Game_Status_UI_Group/Icon_FairyDust/HowmanyDust")
                                    ?.GetComponent<TMPro.TMP_Text>();

            if (fairyDustText != null)
            {
                fairyDustText.text = dustAmount;
                Debug.Log("[UIManager] 순록 UI 갱신: " + dustAmount);
            }
        }
    }

}
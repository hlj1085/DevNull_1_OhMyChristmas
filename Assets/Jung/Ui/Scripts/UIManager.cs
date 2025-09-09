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
}
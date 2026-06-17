using UnityEngine;
using UnityEngine.UI;

public class ClosePopupButton : MonoBehaviour
{
    private PopupManager popupManager;
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }
    }

    private void Start()
    {
        if (PopupManager.instance != null)
            popupManager = PopupManager.instance;
    }

    private void ClosePopup()
    {
        popupManager.CloseActivePopup();
    }
}
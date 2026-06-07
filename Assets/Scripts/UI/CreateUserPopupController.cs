using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateUserPopupController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;

    [Header("UI")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        if (nameInputField != null)
        {
            nameInputField.text = string.Empty;
            nameInputField.Select();
        }

        SetError(string.Empty);

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(CreateUser);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    private void OnDisable()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(CreateUser);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }
    }

    private void CreateUser()
    {
        string playerName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            SetError("Enter a player name.");
            return;
        }

        if (UserManager.instance == null)
        {
            SetError("User Manager was not found.");
            return;
        }

        UserManager.instance.CreateUser(playerName);

        if (popupManager != null)
        {
            popupManager.OpenPopup(PopupId.UserInfo);
        }
    }

    private void ClosePopup()
    {
        if (popupManager != null)
        {
            popupManager.CloseActivePopup();
        }
    }

    private void SetError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
        }
    }
}
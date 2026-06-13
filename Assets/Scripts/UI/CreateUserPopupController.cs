using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateUserPopupController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private UIIconManager iconManager;
    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Input")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text errorText;

    [Header("Buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button changeIconButton;

    [Header("Icon Preview")]
    [SerializeField] private Image selectedIconImage;

    private const int MinPlayerNameCharacters = 3;
    private const int MaxPlayerNameCharacters = 20;

    private string selectedIconId = string.Empty;

    private void OnEnable()
    {
        SetupDefaultIcon();
        ResetInput();
        ClearError();

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(CreateUser);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (changeIconButton != null)
        {
            changeIconButton.onClick.AddListener(OpenIconSelectPopup);
        }

        if (nameInputField != null)
        {
            nameInputField.lineType = TMP_InputField.LineType.SingleLine;
            nameInputField.characterLimit = MaxPlayerNameCharacters;
            nameInputField.onValueChanged.AddListener(OnNameInputChanged);
            nameInputField.onSubmit.AddListener(OnNameInputSubmit);
            nameInputField.Select();
            nameInputField.ActivateInputField();
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

        if (changeIconButton != null)
        {
            changeIconButton.onClick.RemoveListener(OpenIconSelectPopup);
        }

        if (nameInputField != null)
        {
            nameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
            nameInputField.onSubmit.RemoveListener(OnNameInputSubmit);
        }

        ClearError();
    }

    public void OpenIconSelectPopup()
    {
        ClearError();

        if (iconSelectPopupController == null)
        {
            ShowError("Icon select popup was not found.");
            Debug.LogWarning("CreateUserPopupController needs IconSelectPopupController assigned.");
            return;
        }

        iconSelectPopupController.OpenForSelection(selectedIconId, OnIconSelected);
    }

    private void OnIconSelected(UserIconData iconData)
    {
        if (iconData == null)
        {
            return;
        }

        selectedIconId = iconData.IconId;

        RefreshSelectedIconImage();
        ClearError();
    }

    private void CreateUser()
    {
        ClearError();

        string playerName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;

        if (!IsValidPlayerName(playerName))
        {
            ShowError(GetPlayerNameErrorMessage());
            return;
        }

        if (UserManager.instance == null)
        {
            ShowError("User Manager was not found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedIconId))
        {
            SetupDefaultIcon();
        }

        if (string.IsNullOrWhiteSpace(selectedIconId))
        {
            ShowError("No player icons are set up.");
            return;
        }

        UserManager.instance.CreateUser(playerName, selectedIconId);

        if (popupManager != null)
        {
            popupManager.OpenPopup(PopupId.UserInfo);
        }
    }

    private void SetupDefaultIcon()
    {
        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        selectedIconId = iconManager != null
            ? iconManager.GetFirstPlayerIconId()
            : string.Empty;

        RefreshSelectedIconImage();
    }

    private void RefreshSelectedIconImage()
    {
        if (selectedIconImage == null)
        {
            return;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        Sprite iconSprite = iconManager != null
            ? iconManager.GetPlayerIconSpriteById(selectedIconId)
            : null;

        selectedIconImage.sprite = iconSprite;
        selectedIconImage.enabled = iconSprite != null;
        selectedIconImage.preserveAspect = true;
    }

    private void ResetInput()
    {
        if (nameInputField != null)
        {
            nameInputField.text = string.Empty;
        }
    }

    private void OnNameInputChanged(string value)
    {
        ClearError();
    }

    private void OnNameInputSubmit(string value)
    {
        CreateUser();
    }

    private void ClosePopup()
    {
        ClearError();

        if (popupManager != null)
        {
            popupManager.CloseActivePopup();
        }
    }

    private bool IsValidPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return false;
        }

        playerName = playerName.Trim();

        return playerName.Length >= MinPlayerNameCharacters &&
               playerName.Length <= MaxPlayerNameCharacters;
    }

    private string GetPlayerNameErrorMessage()
    {
        return $"Player name must be {MinPlayerNameCharacters}-{MaxPlayerNameCharacters} characters.";
    }

    private void ShowError(string message)
    {
        if (errorText == null)
        {
            return;
        }

        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    private void ClearError()
    {
        if (errorText == null)
        {
            return;
        }

        errorText.text = string.Empty;
        errorText.gameObject.SetActive(false);
    }
}
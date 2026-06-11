using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoPopupController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private UIIconManager iconManager;
    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Icon Section")]
    [SerializeField] private Image userIconImage;
    [SerializeField] private Button changeIconButton;

    [Header("Name View Section")]
    [SerializeField] private GameObject nameViewGroup;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private Button updateNameButton;

    [Header("Name Edit Section")]
    [SerializeField] private GameObject nameEditGroup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button saveNameButton;
    [SerializeField] private Button cancelNameButton;
    [SerializeField] private TMP_Text nameErrorText;

    [Header("Stats Section")]
    [SerializeField] private TMP_Text statsText;

    [Header("Bottom Buttons")]
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        SetNameEditMode(false);
        ClearNameError();
        RefreshDisplay();

        if (changeIconButton != null)
        {
            changeIconButton.onClick.AddListener(OpenIconSelectPopup);
        }

        if (updateNameButton != null)
        {
            updateNameButton.onClick.AddListener(BeginNameEdit);
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.AddListener(SaveNameEdit);
        }

        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.AddListener(CancelNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (nameInputField != null)
        {
            nameInputField.lineType = TMP_InputField.LineType.SingleLine;
            nameInputField.onValueChanged.AddListener(OnNameInputChanged);
            nameInputField.onSubmit.AddListener(OnNameInputSubmit);
        }

        UserManager.UserChanged += RefreshDisplay;
        SaveManager.SaveDataChanged += RefreshDisplay;
    }

    private void OnDisable()
    {
        if (changeIconButton != null)
        {
            changeIconButton.onClick.RemoveListener(OpenIconSelectPopup);
        }

        if (updateNameButton != null)
        {
            updateNameButton.onClick.RemoveListener(BeginNameEdit);
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.RemoveListener(SaveNameEdit);
        }

        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.RemoveListener(CancelNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }

        if (nameInputField != null)
        {
            nameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
            nameInputField.onSubmit.RemoveListener(OnNameInputSubmit);
        }

        UserManager.UserChanged -= RefreshDisplay;
        SaveManager.SaveDataChanged -= RefreshDisplay;

        ClearNameError();
        SetNameEditMode(false);
    }

    public void OpenIconSelectPopup()
    {
        ClearNameError();

        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        if (iconSelectPopupController == null)
        {
            ShowNameError("Icon select popup was not found.");
            Debug.LogWarning("UserInfoPopupController needs IconSelectPopupController assigned.");
            return;
        }

        iconSelectPopupController.OpenForSelection(userManager.CurrentUser.iconId, OnIconSelected);
    }

    private void OnIconSelected(UserIconData iconData)
    {
        if (iconData == null)
        {
            return;
        }

        if (UserManager.instance != null)
        {
            UserManager.instance.ChangeIcon(iconData.IconId);
        }

        RefreshDisplay();
    }

    private void BeginNameEdit()
    {
        ClearNameError();

        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        if (nameInputField != null)
        {
            nameInputField.text = userManager.CurrentUser.playerName;
        }

        SetNameEditMode(true);

        if (nameInputField != null)
        {
            nameInputField.ActivateInputField();
            MoveNameInputCaretToEnd();
        }
    }

    private void MoveNameInputCaretToEnd()
    {
        if (nameInputField == null)
        {
            return;
        }

        int endPosition = nameInputField.text.Length;

        nameInputField.caretPosition = endPosition;
        nameInputField.selectionAnchorPosition = endPosition;
        nameInputField.selectionFocusPosition = endPosition;
    }

    private void SaveNameEdit()
    {
        ClearNameError();

        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        string newPlayerName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(newPlayerName))
        {
            ShowNameError("Enter a player name.");
            return;
        }

        userManager.ChangePlayerName(newPlayerName);

        SetNameEditMode(false);
        RefreshDisplay();
    }

    private void CancelNameEdit()
    {
        ClearNameError();
        SetNameEditMode(false);
        RefreshDisplay();
    }

    private void OnNameInputChanged(string value)
    {
        ClearNameError();
    }

    private void OnNameInputSubmit(string value)
    {
        if (nameEditGroup != null && nameEditGroup.activeSelf)
        {
            SaveNameEdit();
        }
    }

    private void RefreshDisplay()
    {
        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            if (playerNameText != null)
            {
                playerNameText.text = "No user saved";
            }

            if (playerIdText != null)
            {
                playerIdText.text = string.Empty;
            }

            if (statsText != null)
            {
                statsText.text = string.Empty;
            }

            if (userIconImage != null)
            {
                userIconImage.sprite = null;
                userIconImage.enabled = false;
            }

            return;
        }

        UserData userData = userManager.CurrentUser;

        if (playerNameText != null)
        {
            playerNameText.text = userData.playerName;
        }

        if (playerIdText != null)
        {
            playerIdText.text = $"ID: {userData.userId}";
        }

        if (statsText != null)
        {
            statsText.richText = true;
            statsText.text = BuildStatsDisplayText(userData.stats);
        }

        RefreshUserIcon(userData.iconId);
    }

    private void RefreshUserIcon(string iconId)
    {
        if (userIconImage == null)
        {
            return;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        Sprite iconSprite = iconManager != null
            ? iconManager.GetPlayerIconSpriteById(iconId)
            : null;

        userIconImage.sprite = iconSprite;
        userIconImage.enabled = iconSprite != null;
        userIconImage.preserveAspect = true;
    }

    private string BuildStatsDisplayText(UserStats stats)
    {
        if (stats == null)
        {
            return "No stats yet";
        }

        string rowOne = BuildStatsRow("Points", stats.points.ToString(), "Games Played", stats.gamesPlayed.ToString());
        string rowTwo = BuildStatsRow("Wins", stats.wins.ToString(), "Losses", stats.losses.ToString());
        string rowThree = BuildStatsRow("Win Rate", $"{stats.WinRatePercent}%", "Bingos Called", stats.bingosCalled.ToString());

        return
            "<mspace=0.58em>" +
            rowOne + "\n" +
            rowTwo + "\n" +
            rowThree +
            "</mspace>";
    }

    private string BuildStatsRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        string left = $"{leftLabel,-12}{leftValue,-7}";
        string right = $"{rightLabel,-15}{rightValue}";

        return left + right;
    }

    private void SetNameEditMode(bool editing)
    {
        if (nameViewGroup != null)
        {
            nameViewGroup.SetActive(!editing);
        }
        else
        {
            if (playerNameText != null)
            {
                playerNameText.gameObject.SetActive(!editing);
            }

            if (playerIdText != null)
            {
                playerIdText.gameObject.SetActive(!editing);
            }

            if (updateNameButton != null)
            {
                updateNameButton.gameObject.SetActive(!editing);
            }
        }

        if (nameEditGroup != null)
        {
            nameEditGroup.SetActive(editing);
        }
    }

    private void ShowNameError(string message)
    {
        if (nameErrorText == null)
        {
            return;
        }

        nameErrorText.text = message;
        nameErrorText.gameObject.SetActive(true);
    }

    private void ClearNameError()
    {
        if (nameErrorText == null)
        {
            return;
        }

        nameErrorText.text = string.Empty;
        nameErrorText.gameObject.SetActive(false);
    }

    private void ClosePopup()
    {
        ClearNameError();
        SetNameEditMode(false);

        if (popupManager != null)
        {
            popupManager.CloseActivePopup();
        }
    }
}
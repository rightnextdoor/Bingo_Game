using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoPopupController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private UIIconManager iconManager;
    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Text")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statsText;

    [Header("Icon")]
    [SerializeField] private Image userIconImage;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        RefreshDisplay();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        UserManager.UserChanged += RefreshDisplay;
        SaveManager.SaveDataChanged += RefreshDisplay;
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }

        UserManager.UserChanged -= RefreshDisplay;
        SaveManager.SaveDataChanged -= RefreshDisplay;
    }

    public void OpenIconSelectPopup()
    {
        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            return;
        }

        if (iconSelectPopupController == null)
        {
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

    private void RefreshDisplay()
    {
        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            if (playerNameText != null)
            {
                playerNameText.text = "No user saved";
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
            playerNameText.text =
                $"Player: {userData.playerName}\n" +
                $"User ID: {userData.userId}";
        }

        if (statsText != null)
        {
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
    }

    private string BuildStatsDisplayText(UserStats stats)
    {
        if (stats == null)
        {
            return "No stats yet";
        }

        return
            $"Points: {stats.points}\n" +
            $"Games Played: {stats.gamesPlayed}\n" +
            $"Wins: {stats.wins}\n" +
            $"Win Rate: {stats.WinRatePercent}%\n" +
            $"Bingos Called: {stats.bingosCalled}";
    }

    private void ClosePopup()
    {
        if (popupManager != null)
        {
            popupManager.CloseActivePopup();
        }
    }
}
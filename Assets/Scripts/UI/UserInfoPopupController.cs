using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoPopupController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;

    [Header("Text")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statsText;

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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalIconBarController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private PopupManager popupManager;

    [Header("Icon Buttons")]
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button userButton;

    [Header("User Display")]
    [SerializeField] private TMP_Text userNameText;
    [SerializeField] private GameObject emptyUserIconVisual;
    [SerializeField] private GameObject savedUserIconVisual;

    private void OnEnable()
    {
        if (leaderboardButton != null)
        {
            leaderboardButton.onClick.AddListener(OpenLeaderboardPopup);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettingsPopup);
        }

        if (userButton != null)
        {
            userButton.onClick.AddListener(OpenUserPopup);
        }

        LocalUserProfile.UserProfileChanged += RefreshUserDisplay;

        RefreshUserDisplay();
    }

    private void OnDisable()
    {
        if (leaderboardButton != null)
        {
            leaderboardButton.onClick.RemoveListener(OpenLeaderboardPopup);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettingsPopup);
        }

        if (userButton != null)
        {
            userButton.onClick.RemoveListener(OpenUserPopup);
        }

        LocalUserProfile.UserProfileChanged -= RefreshUserDisplay;
    }

    private void OpenLeaderboardPopup()
    {
        popupManager.OpenPopup(PopupId.Leaderboard);
    }

    private void OpenSettingsPopup()
    {
        popupManager.OpenPopup(PopupId.Settings);
    }

    private void OpenUserPopup()
    {
        if (LocalUserProfile.HasUser)
        {
            popupManager.OpenPopup(PopupId.UserInfo);
        }
        else
        {
            popupManager.OpenPopup(PopupId.CreateUser);
        }
    }

    private void RefreshUserDisplay()
    {
        bool hasUser = LocalUserProfile.HasUser;

        if (userNameText != null)
        {
            userNameText.text = hasUser ? LocalUserProfile.PlayerName : string.Empty;
        }

        if (emptyUserIconVisual != null)
        {
            emptyUserIconVisual.SetActive(!hasUser);
        }

        if (savedUserIconVisual != null)
        {
            savedUserIconVisual.SetActive(hasUser);
        }
    }
}
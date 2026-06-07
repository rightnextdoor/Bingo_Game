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
            leaderboardButton.onClick.AddListener(ToggleLeaderboardPopup);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(ToggleSettingsPopup);
        }

        if (userButton != null)
        {
            userButton.onClick.AddListener(ToggleUserPopup);
        }

        UserManager.UserChanged += RefreshUserDisplay;
        SaveManager.SaveDataChanged += RefreshUserDisplay;

        RefreshUserDisplay();
    }

    private void OnDisable()
    {
        if (leaderboardButton != null)
        {
            leaderboardButton.onClick.RemoveListener(ToggleLeaderboardPopup);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ToggleSettingsPopup);
        }

        if (userButton != null)
        {
            userButton.onClick.RemoveListener(ToggleUserPopup);
        }

        UserManager.UserChanged -= RefreshUserDisplay;
        SaveManager.SaveDataChanged -= RefreshUserDisplay;
    }

    private void ToggleLeaderboardPopup()
    {
        if (popupManager != null)
        {
            popupManager.TogglePopup(PopupId.Leaderboard);
        }
    }

    private void ToggleSettingsPopup()
    {
        if (popupManager != null)
        {
            popupManager.TogglePopup(PopupId.Settings);
        }
    }

    private void ToggleUserPopup()
    {
        if (popupManager == null)
        {
            return;
        }

        UserManager userManager = UserManager.instance;

        if (userManager != null && userManager.HasUser)
        {
            popupManager.TogglePopup(PopupId.UserInfo);
        }
        else
        {
            popupManager.TogglePopup(PopupId.CreateUser);
        }
    }

    private void RefreshUserDisplay()
    {
        UserManager userManager = UserManager.instance;
        bool hasUser = userManager != null && userManager.HasUser;

        if (userNameText != null)
        {
            userNameText.text = hasUser ? userManager.PlayerName : string.Empty;
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
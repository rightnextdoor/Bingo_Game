using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameRejoinController : MonoBehaviour
{
    private enum RejoinSource
    {
        Network,
        SavedSolo
    }

    [Header("Text")]
    [SerializeField] private TMP_Text gameDisplayText;

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private bool isDecliningGame;
    private bool isRejoiningGame;
    private RejoinSource rejoinSource;
    private string gameDisplayTitle = string.Empty;
    private Action savedSoloDeclinedAction;

    public void ConfigureNetworkRejoin(string displayTitle)
    {
        rejoinSource = RejoinSource.Network;
        gameDisplayTitle = displayTitle ?? string.Empty;
        savedSoloDeclinedAction = null;
        ApplyDisplayTitle();
    }

    public void ConfigureSavedSoloRejoin(
        string displayTitle,
        Action declinedAction)
    {
        rejoinSource = RejoinSource.SavedSolo;
        gameDisplayTitle = displayTitle ?? string.Empty;
        savedSoloDeclinedAction = declinedAction;
        ApplyDisplayTitle();
    }

    private void OnEnable()
    {
        isDecliningGame = false;
        isRejoiningGame = false;
        SetButtonsInteractable(true);
        ApplyDisplayTitle();

        if (yesButton != null)
        {
            yesButton.onClick.AddListener(RejoinLastGame);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(DeclineLastGame);
        }
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(RejoinLastGame);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(DeclineLastGame);
        }
    }

    private async void RejoinLastGame()
    {
        if (isRejoiningGame)
        {
            return;
        }

        if (rejoinSource == RejoinSource.SavedSolo)
        {
            RejoinSavedSoloGame();
            return;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        string lastGameId = userData?.lastGameId;

        if (string.IsNullOrWhiteSpace(lastGameId) ||
            GameSessionManager.instance == null ||
            GameSceneManager.instance == null)
        {
            return;
        }

        isRejoiningGame = true;
        SetButtonsInteractable(false);
        GameSessionResult rejoinCheck;

        try
        {
            rejoinCheck = await GameSessionManager.instance.CheckNetworkRejoinAsync(lastGameId);
        }
        finally
        {
            isRejoiningGame = false;
        }

        if (rejoinCheck?.success != true)
        {
            SetButtonsInteractable(true);

            if (rejoinCheck?.failureType == GameSessionFailureType.GameNotFound ||
                rejoinCheck?.failureType == GameSessionFailureType.PlayerNotFound ||
                rejoinCheck?.failureType == GameSessionFailureType.PlayerNotEligible)
            {
                UserManager.instance?.ClearLastGameId();
                PopupManager.instance?.CloseActivePopup();
                PopupManager.instance?.OpenFailurePopup("This game has ended and can no longer be rejoined.");
            }
            else
            {
                PopupManager.instance?.OpenFailurePopup("Unable to rejoin the game. Please check your connection.");
            }

            return;
        }

        if (!GameSessionManager.instance.PrepareLastGameRejoin(lastGameId))
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSceneManager.instance.LoadGameScene();
        GameSessionManager.instance.BeginPendingGameEntry();
    }

    private void RejoinSavedSoloGame()
    {
        if (GameSessionManager.instance == null ||
            GameSceneManager.instance == null ||
            !GameSessionManager.instance.PrepareSavedSoloRejoin())
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSceneManager.instance.LoadGameScene();
        GameSessionManager.instance.BeginPendingGameEntry();
    }

    private async void DeclineLastGame()
    {
        if (isDecliningGame)
        {
            return;
        }

        isDecliningGame = true;
        SetButtonsInteractable(false);

        try
        {
            if (rejoinSource == RejoinSource.SavedSolo)
            {
                if (GameSessionManager.instance == null)
                {
                    return;
                }

                await GameSessionManager.instance.DeclineSavedSoloGameAsync();
                Action declinedAction = savedSoloDeclinedAction;
                PopupManager.instance?.CloseActivePopup();
                declinedAction?.Invoke();
                return;
            }

            UserData userData = UserManager.instance?.CurrentUser;

            if (GameSessionManager.instance != null)
            {
                await GameSessionManager.instance.ClearPreviousSessionForFreshLobbyEntryAsync();
            }
            else
            {
                if (LobbyManager.instance != null && userData != null)
                {
                    await LobbyManager.instance.ClearPreviousLobbyMembershipAsync(userData);
                }

                UserManager.instance?.ClearLastGameId();
            }

            PopupManager.instance?.CloseActivePopup();
        }
        finally
        {
            isDecliningGame = false;
            SetButtonsInteractable(true);
        }
    }

    private void ApplyDisplayTitle()
    {
        if (gameDisplayText == null)
        {
            return;
        }

        gameDisplayText.text = gameDisplayTitle;
        gameDisplayText.textWrappingMode = TextWrappingModes.Normal;
        gameDisplayText.enableAutoSizing = true;
    }

    private void SetButtonsInteractable(bool isInteractable)
    {
        if (yesButton != null)
        {
            yesButton.interactable = isInteractable;
        }

        if (noButton != null)
        {
            noButton.interactable = isInteractable;
        }
    }
}

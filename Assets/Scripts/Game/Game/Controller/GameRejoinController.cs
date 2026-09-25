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
        isRejoiningGame = true;
        SetButtonsInteractable(false);
        PopupManager.instance?.CloseActivePopup();
        PopupManager.instance?.OpenReconnectPopup("Checking your previous game...");

        try
        {
            if (string.IsNullOrWhiteSpace(lastGameId) ||
                GameSessionManager.instance == null ||
                GameSceneManager.instance == null)
            {
                await ClearUnavailableNetworkGameAsync();
                PopupManager.instance?.CloseReconnectPopup();
                PopupManager.instance?.OpenFailurePopup("This game has ended and can no longer be rejoined.");
                return;
            }

            GameSessionResult rejoinCheck =
                await GameSessionManager.instance.CheckNetworkRejoinAsync(lastGameId);

            if (rejoinCheck?.success != true)
            {
                bool gameEnded = rejoinCheck?.failureType == GameSessionFailureType.GameNotFound ||
                                 rejoinCheck?.failureType == GameSessionFailureType.PlayerNotFound ||
                                 rejoinCheck?.failureType == GameSessionFailureType.PlayerNotEligible;

                if (gameEnded)
                {
                    await ClearUnavailableNetworkGameAsync();
                }

                PopupManager.instance?.CloseReconnectPopup();
                PopupManager.instance?.OpenFailurePopup(gameEnded
                    ? "This game has ended and can no longer be rejoined."
                    : "Unable to rejoin the game. Please check your connection and try again.");
                return;
            }

            if (!GameSessionManager.instance.PrepareLastGameRejoin(lastGameId))
            {
                PopupManager.instance?.CloseReconnectPopup();
                PopupManager.instance?.OpenFailurePopup("Unable to prepare the game for rejoining. Please try again.");
                return;
            }

            PopupManager.instance?.CloseReconnectPopup();
            GameSceneManager.instance.LoadGameScene();
            GameSessionManager.instance.BeginPendingGameEntry();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameRejoinController] Rejoin check failed: {exception.Message}");
            PopupManager.instance?.CloseReconnectPopup();
            PopupManager.instance?.OpenFailurePopup("Unable to rejoin the game. Please try again.");
        }
        finally
        {
            isRejoiningGame = false;
            SetButtonsInteractable(true);
        }
    }

    private static async System.Threading.Tasks.Task ClearUnavailableNetworkGameAsync()
    {
        try
        {
            if (GameSessionManager.instance != null)
            {
                await GameSessionManager.instance.ClearPreviousSessionForFreshLobbyEntryAsync();
            }
        }
        finally
        {
            UserManager.instance?.ClearLastGameId();
        }
    }

    private void RejoinSavedSoloGame()
    {
        if (GameSessionManager.instance == null ||
            GameSceneManager.instance == null ||
            !GameSessionManager.instance.PrepareSavedSoloRejoin())
        {
            PopupManager.instance?.CloseActivePopup();
            PopupManager.instance?.OpenFailurePopup("The saved game could not be rejoined.");
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
        PopupManager.instance?.CloseActivePopup();
        PopupManager.instance?.OpenReconnectPopup("Leaving your previous game...");

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

        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameRejoinController] Could not clear the previous game: {exception.Message}");
            PopupManager.instance?.OpenFailurePopup("The previous game could not be cleared. Please try again.");
        }
        finally
        {
            PopupManager.instance?.CloseReconnectPopup();
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

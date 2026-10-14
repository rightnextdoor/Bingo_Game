using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class GameOverPopupData
{
    public int winnerCount;
    public string winnerDisplayText = string.Empty;
    public GamePlayerStatus localPlayerStatus = GamePlayerStatus.Lost;
    public int localPlayerScore;
    public bool returnToMainMenuOnly;
}

[DisallowMultipleComponent]
public class GameOverPopupController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text winnerHeadingText;
    [SerializeField] private TMP_Text winnerNameText;
    [SerializeField] private TMP_Text localResultText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Buttons")]
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button returnToMainMenuButton;

    private GameOverPopupData popupData;
    private double countdownEndTime;
    private bool hasSubmitted;

    public void SetData(GameOverPopupData data)
    {
        popupData = data ?? new GameOverPopupData();
        ApplyData();
    }

    private void OnEnable()
    {
        hasSubmitted = false;
        SetButtonsInteractable(true);
        ApplyData();

        float countdownSeconds = GameSettings.instance != null
            ? GameSettings.instance.GameOverCountdownSeconds
            : GameSettings.DefaultGameOverCountdownSeconds;
        countdownEndTime = SessionPauseManager.GetCurrentTime() + countdownSeconds;
        UpdateCountdownText();

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void OnDisable()
    {
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.RemoveListener(ReturnToLobby);
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }
    }

    private void Update()
    {
        if (hasSubmitted || SessionPauseManager.IsPaused)
        {
            return;
        }

        UpdateCountdownText();

        if (SessionPauseManager.GetCurrentTime() >= countdownEndTime)
        {
            if (popupData?.returnToMainMenuOnly == true)
            {
                ReturnToMainMenu();
            }
            else
            {
                ReturnToLobby();
            }
        }
    }

    private void ApplyData()
    {
        GameOverPopupData data = popupData ?? new GameOverPopupData();
        bool showWinner = data.winnerCount > 0;

        if (winnerHeadingText != null)
        {
            winnerHeadingText.gameObject.SetActive(showWinner);
            winnerHeadingText.text = data.winnerCount > 1 ? "Winners" : "Winner";
        }

        if (winnerNameText != null)
        {
            bool showWinnerNames = showWinner &&
                                   !string.IsNullOrWhiteSpace(
                                       data.winnerDisplayText);
            winnerNameText.gameObject.SetActive(showWinnerNames);
            winnerNameText.text = showWinnerNames
                ? data.winnerDisplayText
                : string.Empty;
        }

        if (localResultText != null)
        {
            localResultText.text = data.localPlayerStatus == GamePlayerStatus.Won
                ? "You Won"
                : "You Lost";
        }

        if (scoreText != null)
        {
            scoreText.text = $"Score: {Mathf.Max(0, data.localPlayerScore)}";
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.gameObject.SetActive(!data.returnToMainMenuOnly);
        }
    }

    private void UpdateCountdownText()
    {
        if (countdownText == null)
        {
            return;
        }

        int remainingSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt((float)(
                countdownEndTime - SessionPauseManager.GetCurrentTime())));
        countdownText.text = popupData?.returnToMainMenuOnly == true
            ? $"Returning to main menu in {remainingSeconds}"
            : $"Returning to lobby in {remainingSeconds}";
    }

    private void ReturnToLobby()
    {
        if (popupData?.returnToMainMenuOnly == true)
        {
            ReturnToMainMenu();
            return;
        }

        if (!BeginSubmission())
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSessionManager.instance?.ReturnToLobbyAfterCompletedGame();
    }

    private void ReturnToMainMenu()
    {
        if (!BeginSubmission())
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSessionManager.instance?.ReturnToMainMenuAfterCompletedGame();
    }

    private bool BeginSubmission()
    {
        if (hasSubmitted)
        {
            return false;
        }

        hasSubmitted = true;
        SetButtonsInteractable(false);
        return true;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.interactable = interactable;
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.interactable = interactable;
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RiskDecisionPopupController : MonoBehaviour
{
    private const float DecisionCountdownSeconds = 10f;
    private const string HeaderLabel = "Risk Score";

    [Header("Text")]
    [SerializeField] private TMP_Text headerText;

    [Header("Buttons")]
    [SerializeField] private Button keepPlayingButton;
    [SerializeField] private Button endMyGameButton;

    public event Action<bool> DecisionSubmitted;

    private double decisionEndTime;
    private int displayedSeconds = -1;
    private bool decisionSubmitted;

    private void OnEnable()
    {
        decisionSubmitted = false;
        displayedSeconds = -1;
        decisionEndTime = GamePlayTimer.GetCurrentTime() + DecisionCountdownSeconds;
        SetButtonsInteractable(true);
        RefreshHeaderAndTimeout();

        if (keepPlayingButton != null)
        {
            keepPlayingButton.onClick.RemoveListener(KeepPlaying);
            keepPlayingButton.onClick.AddListener(KeepPlaying);
        }

        if (endMyGameButton != null)
        {
            endMyGameButton.onClick.RemoveListener(EndMyGame);
            endMyGameButton.onClick.AddListener(EndMyGame);
        }
    }

    private void Update()
    {
        if (!decisionSubmitted)
        {
            RefreshHeaderAndTimeout();
        }
    }

    private void OnDisable()
    {
        if (keepPlayingButton != null)
        {
            keepPlayingButton.onClick.RemoveListener(KeepPlaying);
        }

        if (endMyGameButton != null)
        {
            endMyGameButton.onClick.RemoveListener(EndMyGame);
        }
    }

    private void KeepPlaying()
    {
        SubmitDecision(false);
    }

    private void EndMyGame()
    {
        SubmitDecision(true);
    }

    private void SubmitDecision(bool endPlayerGame)
    {
        if (decisionSubmitted)
        {
            return;
        }

        decisionSubmitted = true;
        SetButtonsInteractable(false);

        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.ResolveCurrentPlayerRiskDecision(endPlayerGame))
        {
            decisionSubmitted = false;
            SetButtonsInteractable(true);
            return;
        }

        DecisionSubmitted?.Invoke(endPlayerGame);

        if (PopupManager.instance != null)
        {
            PopupManager.instance.CloseActivePopup();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void RefreshHeaderAndTimeout()
    {
        GameSessionData gameSessionData =
            GameSessionManager.instance?.CurrentGameSession;

        if (gameSessionData == null ||
            gameSessionData.gameState == GameSessionState.Completed)
        {
            ClosePopup();
            return;
        }

        int remainingSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt((float)(decisionEndTime - GamePlayTimer.GetCurrentTime())));

        if (remainingSeconds != displayedSeconds)
        {
            displayedSeconds = remainingSeconds;

            if (headerText != null)
            {
                headerText.text = $"{HeaderLabel} - Close in {remainingSeconds}";
            }
        }

        if (remainingSeconds <= 0)
        {
            SubmitDecision(true);
        }
    }

    private void SetButtonsInteractable(bool isInteractable)
    {
        if (keepPlayingButton != null)
        {
            keepPlayingButton.interactable = isInteractable;
        }

        if (endMyGameButton != null)
        {
            endMyGameButton.interactable = isInteractable;
        }
    }

    private void ClosePopup()
    {
        decisionSubmitted = true;
        SetButtonsInteractable(false);

        if (PopupManager.instance != null)
        {
            PopupManager.instance.CloseActivePopup();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

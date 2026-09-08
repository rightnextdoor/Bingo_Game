using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RiskDecisionPopupController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button keepPlayingButton;
    [SerializeField] private Button endMyGameButton;

    public event Action<bool> DecisionSubmitted;

    private void OnEnable()
    {
        SetButtonsInteractable(true);

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
        SetButtonsInteractable(false);

        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.ResolveCurrentPlayerRiskDecision(endPlayerGame))
        {
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
}

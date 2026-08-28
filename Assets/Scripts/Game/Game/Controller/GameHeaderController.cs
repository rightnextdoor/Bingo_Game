using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameHeaderController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Text")]
    [SerializeField] private TMP_Text gameTitleText;
    [SerializeField] private TMP_Text gameTimerText;

    [Header("Buttons")]
    [SerializeField] private Button leaveButton;

    #endregion

    public event Action LeaveRequested;

    #region Unity Lifecycle

    private void Awake()
    {
        ClearHeader();
    }

    private void OnEnable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
    }

    private void OnDisable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }
    }

    #endregion

    #region Display

    public void DisplayGameInfo(GameSessionData gameSessionData, string gameName)
    {
        if (gameSessionData == null)
        {
            ClearHeader();
            return;
        }

        if (gameTitleText != null)
        {
            string resolvedGameName = string.IsNullOrWhiteSpace(gameName)
                ? gameSessionData.gameModeType.ToString()
                : gameName;

            gameTitleText.text = $"{gameSessionData.playMode} - {resolvedGameName}";
        }

        SetLeaveInteractable(true);
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        if (gameTimerText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

        gameTimerText.gameObject.SetActive(true);
        gameTimerText.text = FormatTime(seconds);
    }

    public void HideTimer()
    {
        if (gameTimerText != null)
        {
            gameTimerText.gameObject.SetActive(false);
        }
    }

    public void SetLeaveInteractable(bool isInteractable)
    {
        if (leaveButton != null)
        {
            leaveButton.interactable = isInteractable;
        }
    }

    public void ClearHeader()
    {
        if (gameTitleText != null)
        {
            gameTitleText.text = string.Empty;
        }

        HideTimer();
        SetLeaveInteractable(false);
    }

    #endregion

    #region Button Events

    private void OnLeaveClicked()
    {
        LeaveRequested?.Invoke();
    }

    #endregion

    #region Helpers

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:0}:{seconds:00}";
    }

    #endregion
}

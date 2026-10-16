using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BingoGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class GameOverWinnerData
{
    public int rank;
    public string displayName = string.Empty;
    public int score;
}

[Serializable]
public class GameOverPopupData
{
    public int winnerCount;
    public List<GameOverWinnerData> winners = new();
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

    [Space]
    [Header("Winner List")]
    [SerializeField, Tooltip("Scroll view containing the existing winner text.")] private ScrollRect winnerScrollRect;
    [SerializeField, Min(1f), Tooltip("Reserved width for the existing final placement rank.")] private float winnerRankWidth = 54f;
    [SerializeField, Min(1f), Tooltip("Reserved width for the match score.")] private float winnerScoreWidth = 110f;
    [SerializeField, Min(0f), Tooltip("Space between rank, identity, and score columns.")] private float winnerColumnSpacing = 12f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Smallest permitted size relative to the winner text's prefab font size.")] private float minimumWinnerFontSizeScale = 0.65f;

    [Header("Buttons")]
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button returnToMainMenuButton;

    private GameOverPopupData popupData;
    private double countdownEndTime;
    private bool hasSubmitted;
    private float preferredWinnerFontSize;
    private bool winnerLayoutPending;
    private float lastWinnerWidth = -1f;
    private TMP_FontAsset lastWinnerFont;
    private readonly StringBuilder winnerTextBuilder = new();

    private void Awake()
    {
        if (winnerNameText != null) preferredWinnerFontSize = winnerNameText.fontSize;
    }

    private void LateUpdate()
    {
        if (winnerNameText == null || !winnerNameText.gameObject.activeInHierarchy) return;
        float width = winnerNameText.rectTransform.rect.width;
        if (winnerLayoutPending || !Mathf.Approximately(lastWinnerWidth, width) || lastWinnerFont != winnerNameText.font)
            RefreshWinnerText(width);
    }

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
        if (winnerNameText != null && preferredWinnerFontSize > 0f) winnerNameText.fontSize = preferredWinnerFontSize;
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
        int winnerCount = data.winners != null ? data.winners.Count : 0;
        bool showWinner = winnerCount > 0;

        if (winnerHeadingText != null)
        {
            winnerHeadingText.gameObject.SetActive(showWinner);
            winnerHeadingText.text = winnerCount > 1 ? "Winners" : "Winner";
        }

        if (winnerNameText != null)
        {
            winnerNameText.gameObject.SetActive(showWinner);
            winnerNameText.text = string.Empty;
            winnerLayoutPending = showWinner;
        }

        if (winnerScrollRect != null) winnerScrollRect.gameObject.SetActive(showWinner);

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

    private void RefreshWinnerText(float _width)
    {
        if (_width <= 0f || popupData?.winners == null) return;
        if (preferredWinnerFontSize <= 0f) preferredWinnerFontSize = winnerNameText.fontSize;
        float usableWidth = _width - winnerNameText.margin.x - winnerNameText.margin.z;
        float nameWidth = usableWidth - winnerRankWidth - winnerScoreWidth - winnerColumnSpacing * 2f;
        if (nameWidth <= 0f) return;

        float size = preferredWinnerFontSize;
        float minimumSize = Mathf.Max(1f, preferredWinnerFontSize * minimumWinnerFontSizeScale);
        foreach (GameOverWinnerData winner in popupData.winners)
        {
            if (winner == null) continue;
            size = Mathf.Min(size, FitWinnerColumn(GetWinnerRank(winner), winnerRankWidth, minimumSize).FontSize);
            size = Mathf.Min(size, FitWinnerColumn(winner.displayName, nameWidth, minimumSize).FontSize);
            size = Mathf.Min(size, FitWinnerColumn(winner.score.ToString("N0"), winnerScoreWidth, minimumSize).FontSize);
        }

        winnerTextBuilder.Clear();
        foreach (GameOverWinnerData winner in popupData.winners)
        {
            if (winner == null) continue;
            if (winnerTextBuilder.Length > 0) winnerTextBuilder.Append('\n');
            string score = winner.score.ToString("N0");
            float scoreWidth = FitWinnerColumn(score, winnerScoreWidth, size, size).RequiredSize.x;
            AppendWinnerColumn(0f, GetWinnerRank(winner));
            AppendWinnerColumn(winnerRankWidth + winnerColumnSpacing, GetFittedWinnerName(winner.displayName, nameWidth, size));
            AppendWinnerColumn(usableWidth - scoreWidth, score);
        }

        winnerNameText.enableAutoSizing = false;
        winnerNameText.richText = true;
        winnerNameText.textWrappingMode = TextWrappingModes.NoWrap;
        winnerNameText.maxVisibleLines = int.MaxValue;
        winnerNameText.overflowMode = TextOverflowModes.Ellipsis;
        winnerNameText.alignment = TextAlignmentOptions.TopLeft;
        winnerNameText.fontSize = size;
        winnerNameText.text = winnerTextBuilder.ToString();
        if (winnerScrollRect != null && winnerScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(winnerScrollRect.content);
            winnerScrollRect.StopMovement();
            winnerScrollRect.verticalNormalizedPosition = 1f;
        }
        lastWinnerWidth = _width;
        lastWinnerFont = winnerNameText.font;
        winnerLayoutPending = false;
    }

    private UITextFitResult FitWinnerColumn(string _text, float _width, float _minimumSize, float _preferredSize = -1f)
    {
        Vector4 margin = winnerNameText.margin;
        return UITextFitter.FitSingleLine(winnerNameText, _text, _preferredSize > 0f ? _preferredSize : preferredWinnerFontSize,
            new Vector2(_width + margin.x + margin.z, float.MaxValue), _minimumSize);
    }

    private string GetFittedWinnerName(string _name, float _width, float _size)
    {
        string name = (_name ?? "Player").Replace('\r', ' ').Replace('\n', ' ');
        if (FitWinnerColumn(name, _width, _size, _size).Fits) return name;
        int low = 0;
        int high = name.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (FitWinnerColumn(name.Substring(0, middle) + "…", _width, _size, _size).Fits) low = middle;
            else high = middle - 1;
        }
        return low > 0 ? name.Substring(0, low) + "…" : string.Empty;
    }

    private void AppendWinnerColumn(float _position, string _value)
    {
        winnerTextBuilder.Append("<pos=");
        winnerTextBuilder.Append(_position.ToString("0.##", CultureInfo.InvariantCulture));
        winnerTextBuilder.Append("px><noparse>");
        winnerTextBuilder.Append(_value);
        winnerTextBuilder.Append("</noparse>");
    }

    private static string GetWinnerRank(GameOverWinnerData _winner)
    {
        return _winner.rank > 0 ? $"#{_winner.rank}" : "—";
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

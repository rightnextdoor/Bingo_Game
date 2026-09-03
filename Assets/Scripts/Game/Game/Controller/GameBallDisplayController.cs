using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GameBallAnimationController))]
public class GameBallDisplayController : MonoBehaviour
{
    private GameBallAnimationController animationController;
    private string currentSessionKey = string.Empty;
    private int processedCalledNumberCount;
    private bool hasSession;

    private void Awake()
    {
        animationController = GetComponent<GameBallAnimationController>();
    }

    public void DisplayGameInfo(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || gameSessionData.gamePlayController?.BallController == null)
        {
            ClearDisplay();
            return;
        }

        GameBallController ballController = gameSessionData.gamePlayController.BallController;
        IReadOnlyList<int> calledNumbers = ballController.CalledNumbers;
        string sessionKey = ResolveSessionKey(gameSessionData);

        if (!hasSession || !string.Equals(currentSessionKey, sessionKey, StringComparison.Ordinal))
        {
            BindSession(sessionKey, calledNumbers, gameSessionData.ballCountType);
            return;
        }

        int calledNumberCount = calledNumbers?.Count ?? 0;

        if (calledNumberCount < processedCalledNumberCount)
        {
            BindSession(sessionKey, calledNumbers, gameSessionData.ballCountType);
            return;
        }

        for (int calledIndex = processedCalledNumberCount;
             calledIndex < calledNumberCount;
             calledIndex++)
        {
            if (TryBuildPresentation(
                    calledNumbers[calledIndex],
                    gameSessionData.ballCountType,
                    out GameBallPresentationData presentationData))
            {
                animationController?.EnqueueBall(presentationData);
                SendBallCalledNotification(presentationData);
            }
        }

        processedCalledNumberCount = calledNumberCount;
    }

    public void ClearDisplay()
    {
        currentSessionKey = string.Empty;
        processedCalledNumberCount = 0;
        hasSession = false;
        animationController?.ClearDisplay();
    }

    private void BindSession(
        string sessionKey,
        IReadOnlyList<int> calledNumbers,
        BingoBallCountType ballCountType)
    {
        currentSessionKey = sessionKey;
        processedCalledNumberCount = calledNumbers?.Count ?? 0;
        hasSession = true;

        List<GameBallPresentationData> history = new List<GameBallPresentationData>();
        int firstHistoryIndex = Mathf.Max(0, processedCalledNumberCount - 4);

        for (int calledIndex = firstHistoryIndex;
             calledIndex < processedCalledNumberCount;
             calledIndex++)
        {
            if (TryBuildPresentation(
                    calledNumbers[calledIndex],
                    ballCountType,
                    out GameBallPresentationData presentationData))
            {
                history.Add(presentationData);
            }
        }

        animationController?.ShowHistoryImmediately(history);
    }

    private bool TryBuildPresentation(
        int number,
        BingoBallCountType ballCountType,
        out GameBallPresentationData presentationData)
    {
        int columnIndex = BingoNumberRangeUtility.GetColumnIndex(number, ballCountType);
        char letter = BingoNumberRangeUtility.GetColumnLetter(columnIndex);

        if (columnIndex < 0 || letter == '\0')
        {
            presentationData = default;
            Debug.LogWarning(
                $"[GameBallDisplayController] Ball {number} is outside the {ballCountType} range.",
                this);
            return false;
        }

        presentationData = new GameBallPresentationData(
            number,
            letter,
            GetLetterColor(columnIndex));
        return true;
    }

    private Color GetLetterColor(int columnIndex)
    {
        UIThemeManager themeManager = UIThemeManager.instance;

        if (themeManager == null)
        {
            return Color.black;
        }

        UIThemeBoardStyle boardStyle = themeManager.ApplyTheme(
            UIThemeSectionType.Board,
            UIThemeBoardType.Default) as UIThemeBoardStyle;

        UIThemeStyle letterStyle = columnIndex switch
        {
            0 => boardStyle?.BHeaderText,
            1 => boardStyle?.IHeaderText,
            2 => boardStyle?.NHeaderText,
            3 => boardStyle?.GHeaderText,
            4 => boardStyle?.OHeaderText,
            _ => null
        };

        return letterStyle != null
            ? letterStyle.VertexColor
            : Color.black;
    }

    private static void SendBallCalledNotification(GameBallPresentationData presentationData)
    {
        if (NotificationService.instance == null)
        {
            return;
        }

        UIMessageData messageData =
            UIMessageCatalog.instance?.GetMessage(UIMessageType.BallCalled);
        string letterColor = ColorUtility.ToHtmlStringRGBA(presentationData.LetterColor);
        string numberColor = messageData != null
            ? messageData.GetNumberColorHex()
            : ColorUtility.ToHtmlStringRGBA(Color.black);

        Dictionary<string, string> replacements = new Dictionary<string, string>
        {
            ["letter"] = $"<color=#{letterColor}>{presentationData.Letter}</color>",
            ["number"] = $"<color=#{numberColor}>{presentationData.Number}</color>"
        };

        string message = messageData?.BuildMessage(replacements);

        NotificationService.instance.SendLocal(UIMessageType.BallCalled, message);
    }

    private static string ResolveSessionKey(GameSessionData gameSessionData)
    {
        if (!string.IsNullOrWhiteSpace(gameSessionData.gameId))
        {
            return gameSessionData.gameId;
        }

        return gameSessionData.lobbyId ?? string.Empty;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class GameController
{
    private Coroutine localAutomaticSequence;
    private int localAutomaticLastCallId;
    private string localAutomaticSessionId = string.Empty;

    private void UpdateLocalAutomaticSequence()
    {
        GameSessionData session = GameSessionManager.instance?.CurrentGameSession;
        GamePlayerData player = session?.GetPlayer(UserManager.instance?.UserId);
        if (!GameAutomaticBoardAuthority.UsesLocalSequence(player) ||
            player.gameStatus != GamePlayerStatus.Eligible || session.gameState != GameSessionState.InProgress ||
            SessionPauseManager.IsPaused || isBingoCheckPending || localAutomaticSequence != null)
            return;
        if (!string.Equals(localAutomaticSessionId, session.gameId, StringComparison.Ordinal))
        {
            localAutomaticSessionId = session.gameId;
            localAutomaticLastCallId = player.automaticBoardLastProcessedBallCallId;
        }
        int count = session.gamePlayController?.BallController?.CalledNumbers?.Count ?? 0;
        if (localAutomaticLastCallId >= count) return;
        localAutomaticSequence = StartCoroutine(RunLocalAutomaticSequence(session.gameId,
            localAutomaticLastCallId + 1));
    }

    private IEnumerator RunLocalAutomaticSequence(string _gameId, int _callId)
    {
        // Ensure the coroutine handle is assigned even when a ball has no matching cell.
        yield return null;
        GameSessionData session = GameSessionManager.instance?.CurrentGameSession;
        GamePlayerData player = session?.GetPlayer(UserManager.instance?.UserId);
        if (!IsLocalAutomaticSequenceValid(_gameId, player)) { localAutomaticSequence = null; yield break; }
        int number = session.gamePlayController.BallController.CalledNumbers[_callId - 1];

        double presentationEnd;
        while (ballDisplayController != null &&
               !ballDisplayController.TryGetBallPresentationEndTime(number, out presentationEnd))
            yield return null;
        while (SessionPauseManager.IsPaused) yield return null;
        player = GameSessionManager.instance?.GetCurrentPlayer();
        if (!IsLocalAutomaticSequenceValid(_gameId, player)) { localAutomaticSequence = null; yield break; }
        int cellIndex = player.boardData.cellNumbers.IndexOf(number);
        if (cellIndex >= 0 && player.markedCellIndices?.Contains(cellIndex) != true)
        {
            float minimum = GameSettings.instance != null ? GameSettings.instance.AutomaticMarkDelayMinimumSeconds : GameSettings.DefaultAutomaticMarkDelayMinimumSeconds;
            float maximum = GameSettings.instance != null ? GameSettings.instance.AutomaticMarkDelayMaximumSeconds : GameSettings.DefaultAutomaticMarkDelayMaximumSeconds;
            double markDue = GamePlayTimer.GetCurrentTime() + UnityEngine.Random.Range(minimum, maximum);

            while (SessionPauseManager.IsPaused || GamePlayTimer.GetCurrentTime() < markDue) yield return null;
            player = GameSessionManager.instance?.GetCurrentPlayer();
            if (!IsLocalAutomaticSequenceValid(_gameId, player)) { localAutomaticSequence = null; yield break; }
            int pulses = GameSettings.instance != null ? GameSettings.instance.AutomaticMarkPulseCount : GameSettings.DefaultAutomaticMarkPulseCount;
            float pulseSeconds = GameSettings.instance != null ? GameSettings.instance.AutomaticMarkPulseSeconds : GameSettings.DefaultAutomaticMarkPulseSeconds;

            for (int i = 0; i < pulses; i++)
            {
                boardSectionController?.SetAutomaticMarkHighlight(cellIndex, true);
                yield return SessionPauseManager.WaitForSeconds(pulseSeconds);
                boardSectionController?.SetAutomaticMarkHighlight(cellIndex, false);
                yield return SessionPauseManager.WaitForSeconds(pulseSeconds);
            }
            player = GameSessionManager.instance?.GetCurrentPlayer();
            if (!IsLocalAutomaticSequenceValid(_gameId, player)) { localAutomaticSequence = null; yield break; }
            boardSectionController?.SetCellMarked(cellIndex, true);
            player.TrySetMarkedCell(cellIndex, true);
            presentedAutomaticMarks.Add(cellIndex);


            float bingoMinimum = GameSettings.instance != null ? GameSettings.instance.AutomaticBingoDelayMinimumSeconds : GameSettings.DefaultAutomaticBingoDelayMinimumSeconds;
            float bingoMaximum = GameSettings.instance != null ? GameSettings.instance.AutomaticBingoDelayMaximumSeconds : GameSettings.DefaultAutomaticBingoDelayMaximumSeconds;
            double bingoDue = GamePlayTimer.GetCurrentTime() + UnityEngine.Random.Range(bingoMinimum, bingoMaximum);

            OnGamePlayerMarkedCellChanged(new GamePlayerMarkedCellChangedData(_gameId, player.userId, cellIndex, true, true));
            GameSessionManager.instance.ReportLocalAutomaticBoardProgress(_callId, true);

            session = GameSessionManager.instance.CurrentGameSession;
            player = session.GetPlayer(UserManager.instance.UserId);
            List<int> prefix = new List<int>(_callId);
            for (int i = 0; i < _callId; i++) prefix.Add(session.gamePlayController.BallController.CalledNumbers[i]);
            bool hasBingo = session.gamePlayController.GetCompletedAvailablePatterns(player.userId,
                player.boardData, player.markedCellIndices, prefix, session.patternTypes).Count > 0;

            while (SessionPauseManager.IsPaused || GamePlayTimer.GetCurrentTime() < bingoDue) yield return null;

            player = GameSessionManager.instance?.GetCurrentPlayer();
            if (hasBingo && IsLocalAutomaticSequenceValid(_gameId, player))
            {
                isBingoCheckPending = true;

                if (!GameSessionManager.instance.ReportLocalAutomaticBoardProgress(_callId, false))
                {
                    isBingoCheckPending = false;

                }
            }
        }
        else
        {
            GameSessionManager.instance.ReportLocalAutomaticBoardProgress(_callId, true);

        }
        localAutomaticLastCallId = _callId;
        localAutomaticSequence = null;
    }

    private bool IsLocalAutomaticSequenceValid(string _gameId, GamePlayerData _player)
    {
        return string.Equals(GameSessionManager.instance?.CurrentGameSession?.gameId, _gameId, StringComparison.Ordinal) &&
            GameAutomaticBoardAuthority.UsesLocalSequence(_player) && _player.gameStatus == GamePlayerStatus.Eligible &&
            GameSessionManager.instance.CurrentGameSession.gameState == GameSessionState.InProgress;
    }

    private void StopLocalAutomaticSequence()
    {
        if (localAutomaticSequence != null)
        {
            StopCoroutine(localAutomaticSequence);
            localAutomaticSequence = null;

        }
        localAutomaticSessionId = string.Empty;
    }
}

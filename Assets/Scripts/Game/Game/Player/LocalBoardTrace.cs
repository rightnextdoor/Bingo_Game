using System;
using System.Diagnostics;
using UnityEngine;

internal static class LocalBoardTrace
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string _stage, string _userId = null, int _cellIndex = -1,
        int _number = -1, int _ballCallId = -1, string _detail = "", GameSessionData _session = null)
    {
        string localUserId = UserManager.instance?.UserId;
        if (string.IsNullOrWhiteSpace(localUserId) ||
            (_userId != null && !string.Equals(_userId, localUserId, StringComparison.Ordinal)))
            return;
        GameSessionData session = _session ?? GameSessionManager.instance?.CurrentGameSession;
        GamePlayerData player = session?.GetPlayer(localUserId);
        if (player == null) return;
        if (_number < 0 && _cellIndex >= 0 && player.boardData?.cellNumbers != null &&
            _cellIndex < player.boardData.cellNumbers.Count)
            _number = player.boardData.cellNumbers[_cellIndex];
        var calledNumbers = session.gamePlayController?.BallController?.CalledNumbers;
        if (_ballCallId < 0 && _number > 0 && calledNumbers != null)
            for (int i = 0; i < calledNumbers.Count; i++)
                if (calledNumbers[i] == _number) { _ballCallId = i + 1; break; }
        UnityEngine.Debug.Log($"[LocalBoardTrace] stage={_stage} game={session.gameId} user={localUserId} call={_ballCallId} number={_number} cell={_cellIndex} frame={Time.frameCount} time={GamePlayTimer.GetCurrentTime():F3} {_detail}");
    }
}

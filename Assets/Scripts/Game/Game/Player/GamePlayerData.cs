using System;
using System.Collections.Generic;

[Serializable]
public class GamePlayerData
{
    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;

    public bool isLobbyHost;
    public bool isConnected;
    public bool isGameSceneReady;
    public bool canRejoin;
    public GamePlayerReturnState returnState;
    public GamePlayerControlType controlType;
    public bool isAutomaticBoardEnabled;

    public GamePlayerStatus gameStatus;
    public int currentMatchScore;
    public bool areStatisticsFinalized;
    public int finalizedScoreDelta;
    public bool isScorePersisted;
    public bool isSubmitTimerActive;
    public double submitTimerEndTime;
    public bool isRiskDecisionPending;
    public List<int> markedCellIndices;

    public int automaticBoardLastProcessedBallCallId;
    public int automaticBoardPendingCellIndex;
    public int automaticBoardPendingBallCallId;
    public double automaticBoardMarkDueTime;
    public int automaticBoardPendingCheckBallCallId;
    public double automaticBoardCheckDueTime;

    public int deathCheckBallCallId;
    public bool deathCheckSucceeded;
    public bool deathCheckCanWin;
    public List<BingoPatternIdentity> queuedRiskPatterns;
    public List<BingoPatternIdentity> activeRiskSubmitPatterns;
    public List<BingoPatternIdentity> lateRiskPatterns;
    public List<BingoPatternIdentity> pendingRiskCheckPatterns;

    public LobbyBoardData boardData;

    public bool HasValidPlayer =>
        !string.IsNullOrWhiteSpace(userId) &&
        !string.IsNullOrWhiteSpace(playerName) &&
        boardData != null;

    public GamePlayerData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;
        isLobbyHost = false;
        isConnected = false;
        isGameSceneReady = false;
        canRejoin = true;
        returnState = GamePlayerReturnState.Active;
        controlType = GamePlayerControlType.Human;
        isAutomaticBoardEnabled = false;
        gameStatus = GamePlayerStatus.Eligible;
        currentMatchScore = 0;
        areStatisticsFinalized = false;
        finalizedScoreDelta = 0;
        isScorePersisted = false;
        isSubmitTimerActive = false;
        submitTimerEndTime = 0d;
        isRiskDecisionPending = false;
        markedCellIndices = new List<int>();
        automaticBoardLastProcessedBallCallId = 0;
        automaticBoardPendingCellIndex = -1;
        automaticBoardPendingBallCallId = 0;
        automaticBoardMarkDueTime = 0d;
        automaticBoardPendingCheckBallCallId = 0;
        automaticBoardCheckDueTime = 0d;
        deathCheckBallCallId = 0;
        deathCheckSucceeded = false;
        deathCheckCanWin = false;
        queuedRiskPatterns = new List<BingoPatternIdentity>();
        activeRiskSubmitPatterns = new List<BingoPatternIdentity>();
        lateRiskPatterns = new List<BingoPatternIdentity>();
        pendingRiskCheckPatterns = new List<BingoPatternIdentity>();
        boardData = new LobbyBoardData();
    }

    public GamePlayerData(LobbyPlayerData lobbyPlayerData) : this()
    {
        if (lobbyPlayerData?.userData == null)
        {
            return;
        }

        userId = lobbyPlayerData.userData.userId ?? string.Empty;
        userTag = lobbyPlayerData.userData.userTag;
        playerName = lobbyPlayerData.userData.playerName ?? string.Empty;
        iconId = lobbyPlayerData.userData.iconId ?? string.Empty;
        isLobbyHost = lobbyPlayerData.isHost;
        isConnected = true;
        isGameSceneReady = userTag == UserTag.Bot;
        canRejoin = userTag != UserTag.Bot;
        returnState = GamePlayerReturnState.Active;
        controlType = userTag == UserTag.Bot
            ? GamePlayerControlType.Bot
            : GamePlayerControlType.Human;
        isAutomaticBoardEnabled = controlType == GamePlayerControlType.Bot;
        boardData = new LobbyBoardData(lobbyPlayerData.boardData);
        EnsureFreeCellMarked();
    }

    public GamePlayerData(GamePlayerData playerData) : this()
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        userTag = playerData.userTag;
        playerName = playerData.playerName ?? string.Empty;
        iconId = playerData.iconId ?? string.Empty;
        isLobbyHost = playerData.isLobbyHost;
        isConnected = playerData.isConnected;
        isGameSceneReady = playerData.isGameSceneReady;
        canRejoin = playerData.canRejoin;
        returnState = playerData.returnState;
        controlType = playerData.controlType;
        isAutomaticBoardEnabled = playerData.isAutomaticBoardEnabled;
        gameStatus = playerData.gameStatus;
        currentMatchScore = playerData.currentMatchScore;
        areStatisticsFinalized = playerData.areStatisticsFinalized;
        finalizedScoreDelta = playerData.finalizedScoreDelta;
        isScorePersisted = playerData.isScorePersisted;
        isSubmitTimerActive = playerData.isSubmitTimerActive;
        submitTimerEndTime = playerData.submitTimerEndTime;
        isRiskDecisionPending = playerData.isRiskDecisionPending;
        markedCellIndices = playerData.markedCellIndices != null
            ? new List<int>(playerData.markedCellIndices)
            : new List<int>();
        automaticBoardLastProcessedBallCallId = playerData.automaticBoardLastProcessedBallCallId;
        automaticBoardPendingCellIndex = playerData.automaticBoardPendingCellIndex;
        automaticBoardPendingBallCallId = playerData.automaticBoardPendingBallCallId;
        automaticBoardMarkDueTime = playerData.automaticBoardMarkDueTime;
        automaticBoardPendingCheckBallCallId = playerData.automaticBoardPendingCheckBallCallId;
        automaticBoardCheckDueTime = playerData.automaticBoardCheckDueTime;
        deathCheckBallCallId = playerData.deathCheckBallCallId;
        deathCheckSucceeded = playerData.deathCheckSucceeded;
        deathCheckCanWin = playerData.deathCheckCanWin;
        queuedRiskPatterns = BingoPatternIdentityList.Clone(playerData.queuedRiskPatterns);
        activeRiskSubmitPatterns = BingoPatternIdentityList.Clone(playerData.activeRiskSubmitPatterns);
        lateRiskPatterns = BingoPatternIdentityList.Clone(playerData.lateRiskPatterns);
        pendingRiskCheckPatterns = BingoPatternIdentityList.Clone(playerData.pendingRiskCheckPatterns);
        boardData = new LobbyBoardData(playerData.boardData);
        RepairControlState();
        EnsureFreeCellMarked();
    }

    public void RepairControlState()
    {
        if (userTag == UserTag.Bot)
        {
            controlType = GamePlayerControlType.Bot;
            isAutomaticBoardEnabled = true;
            canRejoin = false;
            returnState = GamePlayerReturnState.Active;
        }
    }

    public bool TrySetMarkedCell(int cellIndex, bool isMarked)
    {
        if (boardData?.cellNumbers == null ||
            cellIndex < 0 ||
            cellIndex >= boardData.cellNumbers.Count)
        {
            return false;
        }

        markedCellIndices ??= new List<int>();
        bool mustRemainMarked = boardData.usesFreeCell && cellIndex == 12;
        bool resolvedMarked = mustRemainMarked || isMarked;
        bool wasMarked = markedCellIndices.Contains(cellIndex);

        if (resolvedMarked == wasMarked)
        {
            return false;
        }

        if (resolvedMarked)
        {
            markedCellIndices.Add(cellIndex);
            markedCellIndices.Sort();
        }
        else
        {
            markedCellIndices.Remove(cellIndex);
        }

        return true;
    }

    public void EnsureFreeCellMarked()
    {
        markedCellIndices ??= new List<int>();

        if (boardData?.usesFreeCell == true &&
            boardData.cellNumbers != null &&
            boardData.cellNumbers.Count > 12 &&
            !markedCellIndices.Contains(12))
        {
            markedCellIndices.Add(12);
            markedCellIndices.Sort();
        }
    }
}

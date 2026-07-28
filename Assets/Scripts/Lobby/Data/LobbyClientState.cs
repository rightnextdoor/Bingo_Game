using System;
using System.Collections.Generic;

public enum LobbyStateApplyResult
{
    Invalid,
    Ignored,
    Applied,
    RequiresResync
}

public class LobbyClientState
{
    #region Fields

    private readonly Dictionary<string, LobbyBoardData> boardsByUserId = new Dictionary<string, LobbyBoardData>(StringComparer.Ordinal);

    private string lobbyId = string.Empty;
    private LobbyViewData viewData;
    private long revision;

    public string LobbyId => lobbyId;
    public LobbyViewData ViewData => viewData;
    public long Revision => revision;
    public bool HasLobby => !string.IsNullOrWhiteSpace(lobbyId) && viewData != null;

    #endregion

    #region Snapshot State

    public bool SetSnapshot(string expectedLobbyId, LobbyViewData lobbyViewData)
    {
        return SetSnapshot(expectedLobbyId, lobbyViewData, revision);
    }

    public bool SetSnapshot(string expectedLobbyId, LobbyViewData lobbyViewData, long snapshotRevision)
    {
        if (lobbyViewData == null)
        {
            return false;
        }

        string resolvedLobbyId = !string.IsNullOrWhiteSpace(expectedLobbyId) ? expectedLobbyId.Trim() : lobbyViewData.lobbyId?.Trim();

        if (string.IsNullOrWhiteSpace(resolvedLobbyId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(lobbyViewData.lobbyId) && !string.Equals(lobbyViewData.lobbyId, resolvedLobbyId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lobbyViewData.lobbyId = resolvedLobbyId;
        lobbyId = resolvedLobbyId;
        viewData = lobbyViewData;
        revision = Math.Max(0, snapshotRevision);
        return true;
    }

    public bool IsCurrentLobby(string otherLobbyId)
    {
        return HasLobby && !string.IsNullOrWhiteSpace(otherLobbyId) && string.Equals(lobbyId, otherLobbyId, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasPlayer(string userId)
    {
        return FindPlayerIndex(userId) >= 0;
    }

    #endregion

    #region Player Deltas

    public LobbyStateApplyResult ApplyPlayerJoined(LobbyPlayerJoinedData data)
    {
        if (data?.playerData == null || string.IsNullOrWhiteSpace(data.playerData.userId) || viewData?.players == null)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        int existingIndex = FindPlayerIndex(data.playerData.userId);

        if (existingIndex >= 0)
        {
            viewData.players[existingIndex] = data.playerData;
        }
        else if (data.playerData.isHost)
        {
            viewData.players.Insert(0, data.playerData);
        }
        else
        {
            viewData.players.Add(data.playerData);
        }

        viewData.playerCount = data.playerCount;
        viewData.botCount = data.botCount;
        return LobbyStateApplyResult.Applied;
    }

    public LobbyStateApplyResult ApplyPlayerJoinedBatch(LobbyPlayerJoinedBatchData data)
    {
        if (data?.players == null || viewData?.players == null)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        for (int i = 0; i < data.players.Count; i++)
        {
            LobbyPlayerViewData playerData = data.players[i];

            if (playerData == null || string.IsNullOrWhiteSpace(playerData.userId))
            {
                continue;
            }

            int existingIndex = FindPlayerIndex(playerData.userId);

            if (existingIndex >= 0)
            {
                viewData.players[existingIndex] = playerData;
            }
            else if (playerData.isHost)
            {
                viewData.players.Insert(0, playerData);
            }
            else
            {
                viewData.players.Add(playerData);
            }
        }

        viewData.playerCount = data.playerCount;
        viewData.botCount = data.botCount;
        return LobbyStateApplyResult.Applied;
    }

    public LobbyStateApplyResult ApplyPlayerLeft(LobbyPlayerLeftData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.userId) || viewData?.players == null)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        int playerIndex = FindPlayerIndex(data.userId);

        if (playerIndex >= 0)
        {
            viewData.players.RemoveAt(playerIndex);
        }

        boardsByUserId.Remove(data.userId);
        viewData.playerCount = data.playerCount;
        viewData.botCount = data.botCount;
        return LobbyStateApplyResult.Applied;
    }

    public LobbyStateApplyResult ApplyPlayerReadyChanged(LobbyPlayerReadyChangedData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.userId))
        {
            return LobbyStateApplyResult.Invalid;
        }

        int playerIndex = FindPlayerIndex(data.userId);

        if (playerIndex < 0)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        viewData.players[playerIndex].isReady = data.isReady;
        return LobbyStateApplyResult.Applied;
    }

    #endregion

    #region Lobby Deltas

    public LobbyStateApplyResult ApplySettingsChanged(LobbySettingsChangedData data)
    {
        if (data == null || viewData == null)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        viewData.lobbyName = data.lobbyName ?? string.Empty;
        viewData.roomCode = data.roomCode ?? string.Empty;
        viewData.hasPassword = data.hasPassword;
        viewData.lobbyPassword = data.lobbyPassword ?? string.Empty;
        viewData.gameModeType = data.gameModeType;
        viewData.gameModeName = data.gameModeName ?? string.Empty;
        viewData.hasRule = data.hasRule;
        viewData.ruleType = data.ruleType;
        viewData.patternTypes = data.patternTypes != null ? new List<BingoPatternType>(data.patternTypes) : new List<BingoPatternType>();
        viewData.usesDefaultPatterns = data.usesDefaultPatterns;
        viewData.ballCountType = data.ballCountType;
        viewData.useFreeCell = data.useFreeCell;
        viewData.playerCount = data.playerCount;
        viewData.maxPlayers = data.maxPlayers;
        viewData.unlimitedPlayers = data.unlimitedPlayers;
        viewData.addBots = data.addBots;
        viewData.botCount = data.botCount;
        return LobbyStateApplyResult.Applied;
    }

    public LobbyStateApplyResult ApplyStateChanged(LobbyStateChangedData data)
    {
        if (data == null || viewData == null)
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(data.lobbyId, data.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        viewData.lobbyState = data.lobbyState;
        viewData.isTimerActive = data.isTimerActive;
        viewData.timerEndTime = data.timerEndTime;
        return LobbyStateApplyResult.Applied;
    }

    #endregion

    #region Initial Sync

    public bool ApplyInitialSyncBatch(LobbyInitialSyncBatchData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.lobbyId) || !IsCurrentLobby(data.lobbyId))
        {
            return false;
        }

        if (data.resetState)
        {
            if (data.lobbyViewData == null)
            {
                return false;
            }

            data.lobbyViewData.lobbyId = lobbyId;
            data.lobbyViewData.players ??= new List<LobbyPlayerViewData>();
            data.lobbyViewData.players.Clear();
            viewData = data.lobbyViewData;
            boardsByUserId.Clear();
        }

        if (viewData?.players == null)
        {
            return false;
        }

        if (data.players != null)
        {
            for (int i = 0; i < data.players.Count; i++)
            {
                LobbyPlayerViewData playerData = data.players[i];

                if (playerData == null || string.IsNullOrWhiteSpace(playerData.userId))
                {
                    continue;
                }

                int existingIndex = FindPlayerIndex(playerData.userId);

                if (existingIndex >= 0)
                {
                    viewData.players[existingIndex] = playerData;
                }
                else if (playerData.isHost)
                {
                    viewData.players.Insert(0, playerData);
                }
                else
                {
                    viewData.players.Add(playerData);
                }
            }
        }

        if (data.boards != null)
        {
            for (int i = 0; i < data.boards.Count; i++)
            {
                LobbyPlayerBoardViewData boardData = data.boards[i];

                if (boardData != null && !string.IsNullOrWhiteSpace(boardData.userId))
                {
                    boardsByUserId[boardData.userId] = new LobbyBoardData(boardData.boardData);
                }
            }
        }

        if (data.isFinalBatch)
        {
            revision = Math.Max(0, data.revision);
        }

        return true;
    }

    #endregion

    #region Board State

    public bool SetBoardSnapshot(LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData == null || string.IsNullOrWhiteSpace(boardCollectionData.lobbyId))
        {
            return false;
        }

        if (HasLobby && !IsCurrentLobby(boardCollectionData.lobbyId))
        {
            return false;
        }

        boardsByUserId.Clear();
        ApplyBoardValues(boardCollectionData);
        return true;
    }

    public bool ApplyBoardCollection(LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData == null || string.IsNullOrWhiteSpace(boardCollectionData.lobbyId))
        {
            return false;
        }

        if (HasLobby && !IsCurrentLobby(boardCollectionData.lobbyId))
        {
            return false;
        }

        ApplyBoardValues(boardCollectionData);
        return true;
    }

    public LobbyStateApplyResult ApplyNetworkBoardCollection(LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData == null || string.IsNullOrWhiteSpace(boardCollectionData.lobbyId))
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(boardCollectionData.lobbyId, boardCollectionData.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        ApplyBoardValues(boardCollectionData);
        return LobbyStateApplyResult.Applied;
    }

    public bool ApplyBoardUpdate(LobbyPlayerBoardUpdateData updateData)
    {
        if (updateData == null || string.IsNullOrWhiteSpace(updateData.userId) || !IsCurrentLobby(updateData.lobbyId))
        {
            return false;
        }

        boardsByUserId[updateData.userId] = new LobbyBoardData(updateData.boardData);
        return true;
    }

    public LobbyStateApplyResult ApplyNetworkBoardUpdate(LobbyPlayerBoardUpdateData updateData)
    {
        if (updateData == null || string.IsNullOrWhiteSpace(updateData.userId))
        {
            return LobbyStateApplyResult.Invalid;
        }

        LobbyStateApplyResult revisionResult = ValidateRevision(updateData.lobbyId, updateData.revision);

        if (revisionResult != LobbyStateApplyResult.Applied)
        {
            return revisionResult;
        }

        boardsByUserId[updateData.userId] = new LobbyBoardData(updateData.boardData);
        return LobbyStateApplyResult.Applied;
    }

    public LobbyBoardData GetPlayerBoard(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !boardsByUserId.TryGetValue(userId, out LobbyBoardData boardData))
        {
            return null;
        }

        return boardData;
    }

    private void ApplyBoardValues(LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData?.boards == null)
        {
            return;
        }

        for (int i = 0; i < boardCollectionData.boards.Count; i++)
        {
            LobbyPlayerBoardViewData playerBoard = boardCollectionData.boards[i];

            if (playerBoard == null || string.IsNullOrWhiteSpace(playerBoard.userId))
            {
                continue;
            }

            boardsByUserId[playerBoard.userId] = new LobbyBoardData(playerBoard.boardData);
        }
    }

    #endregion

    #region Revision

    private LobbyStateApplyResult ValidateRevision(string incomingLobbyId, long incomingRevision)
    {
        if (!IsCurrentLobby(incomingLobbyId) || incomingRevision < 1)
        {
            return LobbyStateApplyResult.Invalid;
        }

        if (incomingRevision <= revision)
        {
            return LobbyStateApplyResult.Ignored;
        }

        if (incomingRevision != revision + 1)
        {
            return LobbyStateApplyResult.RequiresResync;
        }

        revision = incomingRevision;
        return LobbyStateApplyResult.Applied;
    }

    private int FindPlayerIndex(string userId)
    {
        if (!HasLobby || string.IsNullOrWhiteSpace(userId) || viewData.players == null)
        {
            return -1;
        }

        for (int i = 0; i < viewData.players.Count; i++)
        {
            LobbyPlayerViewData playerData = viewData.players[i];

            if (playerData != null && string.Equals(playerData.userId, userId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    #endregion

    #region Clear

    public void Clear()
    {
        lobbyId = string.Empty;
        viewData = null;
        revision = 0;
        boardsByUserId.Clear();
    }

    #endregion
}

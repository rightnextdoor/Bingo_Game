using System;
using System.Collections.Generic;

public class LobbyClientState
{
    #region Fields

    private readonly Dictionary<string, LobbyBoardData> boardsByUserId = new Dictionary<string, LobbyBoardData>(StringComparer.Ordinal);

    private string lobbyId = string.Empty;
    private LobbyViewData viewData;

    public string LobbyId => lobbyId;
    public LobbyViewData ViewData => viewData;
    public bool HasLobby => !string.IsNullOrWhiteSpace(lobbyId) && viewData != null;

    #endregion

    #region Lobby State

    public bool SetSnapshot(string expectedLobbyId, LobbyViewData lobbyViewData)
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
        return true;
    }

    public bool IsCurrentLobby(string otherLobbyId)
    {
        return HasLobby && !string.IsNullOrWhiteSpace(otherLobbyId) && string.Equals(lobbyId, otherLobbyId, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasPlayer(string userId)
    {
        if (!HasLobby || string.IsNullOrWhiteSpace(userId) || viewData.players == null)
        {
            return false;
        }

        for (int i = 0; i < viewData.players.Count; i++)
        {
            LobbyPlayerViewData playerData = viewData.players[i];

            if (playerData != null && string.Equals(playerData.userId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
        return ApplyBoardCollection(boardCollectionData);
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

        if (boardCollectionData.boards == null)
        {
            return true;
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

        return true;
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

    public LobbyBoardData GetPlayerBoard(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !boardsByUserId.TryGetValue(userId, out LobbyBoardData boardData))
        {
            return null;
        }

        return boardData;
    }

    #endregion

    #region Clear

    public void Clear()
    {
        lobbyId = string.Empty;
        viewData = null;
        boardsByUserId.Clear();
    }

    #endregion
}

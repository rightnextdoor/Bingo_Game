using System;

public class LobbyClientState
{
    #region Fields

    private string lobbyId = string.Empty;
    private LobbyViewData viewData;

    public string LobbyId => lobbyId;
    public LobbyViewData ViewData => viewData;
    public bool HasLobby => !string.IsNullOrWhiteSpace(lobbyId) && viewData != null;

    #endregion

    #region State

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

    public void Clear()
    {
        lobbyId = string.Empty;
        viewData = null;
    }

    #endregion
}

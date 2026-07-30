using System;

[Serializable]
public class LobbyPlayerReadyChangedData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public string userId;
    public bool isReady;

    #endregion

    #region Constructors

    public LobbyPlayerReadyChangedData()
    {
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
        isReady = false;
    }

    public LobbyPlayerReadyChangedData(string lobbyId, long revision, string userId, bool isReady)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.userId = userId ?? string.Empty;
        this.isReady = isReady;
    }

    #endregion
}

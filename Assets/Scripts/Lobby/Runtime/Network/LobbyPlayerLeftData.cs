using System;

[Serializable]
public class LobbyPlayerLeftData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public string userId;
    public int playerCount;
    public int botCount;

    #endregion

    #region Constructors

    public LobbyPlayerLeftData()
    {
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
        playerCount = 0;
        botCount = 0;
    }

    public LobbyPlayerLeftData(string lobbyId, long revision, string userId, int playerCount, int botCount)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.userId = userId ?? string.Empty;
        this.playerCount = playerCount;
        this.botCount = botCount;
    }

    #endregion
}

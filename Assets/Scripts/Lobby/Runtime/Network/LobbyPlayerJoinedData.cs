using System;

[Serializable]
public class LobbyPlayerJoinedData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public LobbyPlayerViewData playerData;
    public int playerCount;
    public int botCount;

    #endregion

    #region Constructors

    public LobbyPlayerJoinedData()
    {
        lobbyId = string.Empty;
        revision = 0;
        playerData = new LobbyPlayerViewData();
        playerCount = 0;
        botCount = 0;
    }

    public LobbyPlayerJoinedData(string lobbyId, long revision, LobbyPlayerViewData playerData, int playerCount, int botCount)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.playerData = playerData ?? new LobbyPlayerViewData();
        this.playerCount = playerCount;
        this.botCount = botCount;
    }

    #endregion
}

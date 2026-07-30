using System;
using System.Collections.Generic;

[Serializable]
public class LobbyPlayerJoinedBatchData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public List<LobbyPlayerViewData> players;
    public int playerCount;
    public int botCount;

    #endregion

    #region Constructors

    public LobbyPlayerJoinedBatchData()
    {
        lobbyId = string.Empty;
        revision = 0;
        players = new List<LobbyPlayerViewData>();
        playerCount = 0;
        botCount = 0;
    }

    public LobbyPlayerJoinedBatchData(string lobbyId, long revision, IEnumerable<LobbyPlayerViewData> players, int playerCount, int botCount) : this()
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.playerCount = playerCount;
        this.botCount = botCount;

        if (players == null)
        {
            return;
        }

        foreach (LobbyPlayerViewData playerData in players)
        {
            if (playerData != null)
            {
                this.players.Add(playerData);
            }
        }
    }

    #endregion
}

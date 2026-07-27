using System;
using System.Collections.Generic;

[Serializable]
public class LobbyHostSettingsData
{
    public BingoGameModeType gameModeType;
    public BingoBallCountType ballCountType;
    public bool useFreeCell;

    public List<BingoPatternType> patternTypes;
    public bool usesDefaultPatterns;

    public bool unlimitedPlayers;
    public int maxPlayers;

    public bool addBots;
    public int botCount;

    public LobbyHostSettingsData()
    {
        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;

        patternTypes = new List<BingoPatternType>();
        usesDefaultPatterns = true;

        unlimitedPlayers = false;
        maxPlayers = LobbySettings.instance != null
            ? LobbySettings.instance.MinimumPlayers
            : 6;

        addBots = false;
        botCount = 0;
    }

    public LobbyHostSettingsData(LobbyViewData lobbyViewData) : this()
    {
        ApplyLobbyViewData(lobbyViewData);
    }

    public void ApplyLobbyViewData(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        gameModeType = lobbyViewData.gameModeType;
        ballCountType = lobbyViewData.ballCountType;
        useFreeCell = lobbyViewData.useFreeCell;

        patternTypes = lobbyViewData.patternTypes != null
            ? new List<BingoPatternType>(lobbyViewData.patternTypes)
            : new List<BingoPatternType>();

        usesDefaultPatterns = lobbyViewData.usesDefaultPatterns;

        unlimitedPlayers = lobbyViewData.unlimitedPlayers;
        maxPlayers = lobbyViewData.maxPlayers;

        addBots = lobbyViewData.addBots;
        botCount = addBots ? 1 : 0;
    }
}

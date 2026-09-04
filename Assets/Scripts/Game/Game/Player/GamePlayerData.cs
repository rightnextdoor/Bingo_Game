using System;

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

    public GamePlayerStatus gameStatus;
    public int currentMatchScore;
    public bool areStatisticsFinalized;
    public int finalizedScoreDelta;
    public bool isScorePersisted;
    public bool isSubmitTimerActive;
    public double submitTimerEndTime;

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
        gameStatus = GamePlayerStatus.Eligible;
        currentMatchScore = 0;
        areStatisticsFinalized = false;
        finalizedScoreDelta = 0;
        isScorePersisted = false;
        isSubmitTimerActive = false;
        submitTimerEndTime = 0d;
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
        boardData = new LobbyBoardData(lobbyPlayerData.boardData);
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
        gameStatus = playerData.gameStatus;
        currentMatchScore = playerData.currentMatchScore;
        areStatisticsFinalized = playerData.areStatisticsFinalized;
        finalizedScoreDelta = playerData.finalizedScoreDelta;
        isScorePersisted = playerData.isScorePersisted;
        isSubmitTimerActive = playerData.isSubmitTimerActive;
        submitTimerEndTime = playerData.submitTimerEndTime;
        boardData = new LobbyBoardData(playerData.boardData);
    }
}

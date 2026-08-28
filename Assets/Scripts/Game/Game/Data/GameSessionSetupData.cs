using System;
using System.Collections.Generic;

[Serializable]
public class GameSessionSetupData
{
    public int dataVersion;
    public string lobbyId;
    public SessionRuntimeType runtimeType;
    public MainMenuPlayMode playMode;

    public string lobbyName;
    public string roomCode;
    public bool hasPassword;
    public string lobbyPassword;

    public BingoGameModeType gameModeType;
    public bool hasRule;
    public BingoRuleType ruleType;
    public List<BingoPatternType> patternTypes;
    public bool usesDefaultPatterns;
    public BingoBallCountType ballCountType;
    public bool useFreeCell;

    public List<GamePlayerData> players;

    public GameSessionSetupData()
    {
        dataVersion = 2;
        lobbyId = string.Empty;
        runtimeType = SessionRuntimeType.Local;
        playMode = MainMenuPlayMode.None;
        lobbyName = string.Empty;
        roomCode = string.Empty;
        hasPassword = false;
        lobbyPassword = string.Empty;
        gameModeType = BingoGameModeType.Traditional;
        hasRule = false;
        ruleType = BingoRuleType.Traditional;
        patternTypes = new List<BingoPatternType>();
        usesDefaultPatterns = true;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        players = new List<GamePlayerData>();
    }

    public GameSessionSetupData(GameSessionSetupData setupData) : this()
    {
        if (setupData == null)
        {
            return;
        }

        dataVersion = setupData.dataVersion;
        lobbyId = setupData.lobbyId ?? string.Empty;
        runtimeType = setupData.runtimeType;
        playMode = setupData.playMode;
        lobbyName = setupData.lobbyName ?? string.Empty;
        roomCode = setupData.roomCode ?? string.Empty;
        hasPassword = setupData.hasPassword;
        lobbyPassword = setupData.lobbyPassword ?? string.Empty;
        gameModeType = setupData.gameModeType;
        hasRule = setupData.hasRule;
        ruleType = setupData.ruleType;
        patternTypes = setupData.patternTypes != null
            ? new List<BingoPatternType>(setupData.patternTypes)
            : new List<BingoPatternType>();
        usesDefaultPatterns = setupData.usesDefaultPatterns;
        ballCountType = setupData.ballCountType;
        useFreeCell = setupData.useFreeCell;

        if (setupData.players == null)
        {
            return;
        }

        for (int i = 0; i < setupData.players.Count; i++)
        {
            players.Add(new GamePlayerData(setupData.players[i]));
        }
    }

    public static GameSessionSetupData FromLobby(Lobby lobby)
    {
        if (lobby?.Controller == null ||
            (lobby.lobbyState != LobbyState.FinalCountdown && lobby.lobbyState != LobbyState.InGame))
        {
            return null;
        }

        LobbyController controller = lobby.Controller;
        LobbyViewData lobbyViewData = controller.BuildViewData(false);
        GameSessionSetupData setupData = new GameSessionSetupData
        {
            lobbyId = lobby.GetLobbyId(),
            runtimeType = lobby.playMode == MainMenuPlayMode.Solo
                ? SessionRuntimeType.Local
                : SessionRuntimeType.Network,
            playMode = lobby.playMode,
            lobbyName = lobbyViewData?.lobbyName ?? controller.LobbyName,
            roomCode = lobbyViewData?.roomCode ?? controller.RoomCode,
            hasPassword = lobbyViewData?.hasPassword ?? controller.HasPassword,
            lobbyPassword = lobbyViewData?.lobbyPassword ?? string.Empty,
            gameModeType = controller.GameModeType,
            hasRule = controller.HasRule,
            ruleType = controller.RuleType,
            patternTypes = controller.PatternTypes != null
                ? new List<BingoPatternType>(controller.PatternTypes)
                : new List<BingoPatternType>(),
            usesDefaultPatterns = controller.UsesDefaultPatterns,
            ballCountType = controller.BallCountType,
            useFreeCell = controller.UseFreeCell
        };

        IReadOnlyList<LobbyPlayerData> lobbyPlayers = controller.Players;

        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            setupData.players.Add(new GamePlayerData(lobbyPlayers[i]));
        }

        return setupData;
    }

    public bool IsValid(out string failureMessage)
    {
        failureMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(lobbyId) || playMode == MainMenuPlayMode.None)
        {
            failureMessage = "The linked Lobby information is missing.";
            return false;
        }

        if ((playMode == MainMenuPlayMode.Solo && runtimeType != SessionRuntimeType.Local) ||
            (playMode != MainMenuPlayMode.Solo && runtimeType != SessionRuntimeType.Network))
        {
            failureMessage = "The Game runtime does not match the Lobby play mode.";
            return false;
        }

        if (players == null || players.Count == 0)
        {
            failureMessage = "The locked Game player list is empty.";
            return false;
        }

        HashSet<string> userIds = new HashSet<string>(StringComparer.Ordinal);
        bool hasHumanPlayer = false;

        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData == null || !playerData.HasValidPlayer ||
                playerData.boardData.cellNumbers == null ||
                playerData.boardData.cellNumbers.Count == 0)
            {
                failureMessage = "A locked Game player or board is invalid.";
                return false;
            }

            if (!userIds.Add(playerData.userId))
            {
                failureMessage = "The locked Game player list contains a duplicate UserId.";
                return false;
            }

            hasHumanPlayer |= playerData.userTag != UserTag.Bot;
        }

        if (!hasHumanPlayer)
        {
            failureMessage = "The Game requires at least one real player when it is created.";
            return false;
        }

        return true;
    }
}

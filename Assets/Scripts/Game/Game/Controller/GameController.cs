using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Sections")]
    [SerializeField] private GameHeaderController headerController;
    [SerializeField] private LobbyPlayerListController playerListController;
    [SerializeField] private GameInfoController gameInfoController;
    [SerializeField] private LobbyCustomPanelController customPanelController;

    #endregion

    #region Private Fields

    private readonly List<PlayerListPlayerData> visiblePlayers = new List<PlayerListPlayerData>();
    private Coroutine bindRoutine;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SubscribeToHeader();
        bindRoutine = StartCoroutine(BindWhenGameIsReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnsubscribeFromHeader();
        UnsubscribeFromGameSessionManager();
    }

    #endregion

    #region Game Display

    public void DisplayGameInfo(GameSessionData gameSessionData)
    {
        if (gameSessionData == null)
        {
            ClearDisplay();
            return;
        }

        GameModeManager gameModeManager = GameModeManager.instance;
        BingoGameModeData gameModeData = gameModeManager != null
            ? gameModeManager.GetGameModeData(gameSessionData.gameModeType)
            : null;

        string gameName = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.GameName)
            ? gameModeData.GameName
            : gameSessionData.gameModeType.ToString();

        headerController?.DisplayGameInfo(gameSessionData, gameName);
        DisplayPlayerList(gameSessionData);
        DisplayGameModeInfo(gameSessionData, gameModeData, gameName, gameModeManager);
        DisplayCustomLobbyInfo(gameSessionData);
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        headerController?.SetTimerSeconds(remainingSeconds);
    }

    public void HideTimer()
    {
        headerController?.HideTimer();
    }

    private void DisplayPlayerList(GameSessionData gameSessionData)
    {
        visiblePlayers.Clear();
        bool localPlayerIsHost = gameSessionData?.GetPlayer(UserManager.instance?.UserId)?.isLobbyHost == true;

        if (gameSessionData?.players != null)
        {
            AddVisibleHost(gameSessionData.players, localPlayerIsHost);

            for (int i = 0; i < gameSessionData.players.Count; i++)
            {
                GamePlayerData playerData = gameSessionData.players[i];

                if (playerData == null || playerData.isLobbyHost)
                {
                    continue;
                }

                AddVisiblePlayer(playerData, localPlayerIsHost);
            }
        }

        playerListController?.DisplayPlayers(visiblePlayers, visiblePlayers.Count);
    }

    private void AddVisibleHost(IReadOnlyList<GamePlayerData> players, bool localPlayerIsHost)
    {
        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData != null && playerData.isLobbyHost)
            {
                AddVisiblePlayer(playerData, localPlayerIsHost);
                return;
            }
        }
    }

    private void AddVisiblePlayer(GamePlayerData gamePlayerData, bool localPlayerIsHost)
    {
        if (gamePlayerData == null || !gamePlayerData.HasValidPlayer || !gamePlayerData.isGameSceneReady)
        {
            return;
        }

        PlayerListPlayerData playerData = new PlayerListPlayerData
        {
            userId = gamePlayerData.userId ?? string.Empty,
            userTag = gamePlayerData.userTag,
            playerName = gamePlayerData.playerName ?? string.Empty,
            iconId = gamePlayerData.iconId ?? string.Empty,
            isHost = gamePlayerData.isLobbyHost,
            isReady = true,
            boardData = new LobbyBoardData(gamePlayerData.boardData),
            canKick = false,
            showBotIcon = localPlayerIsHost && gamePlayerData.userTag == UserTag.Bot,
            showReadyIcon = false
        };

        visiblePlayers.Add(playerData);
    }

    private void DisplayGameModeInfo(
        GameSessionData gameSessionData,
        BingoGameModeData gameModeData,
        string gameName,
        GameModeManager gameModeManager)
    {
        string gameDescription = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.Description)
            ? gameModeData.Description
            : "No game information is available for this game mode.";

        string ruleDescription = string.Empty;

        if (gameSessionData.hasRule)
        {
            BingoGameRuleData ruleData = gameModeManager != null
                ? gameModeManager.GetGameRuleData(gameSessionData.ruleType)
                : null;

            ruleDescription = ruleData != null && !string.IsNullOrWhiteSpace(ruleData.Description)
                ? ruleData.Description
                : "No rule description is available for this game mode.";
        }

        gameInfoController?.ShowGameInfo(
            gameName,
            gameDescription,
            gameSessionData.ballCountType,
            gameSessionData.hasRule,
            ruleDescription,
            gameSessionData.patternTypes);
    }

    private void DisplayCustomLobbyInfo(GameSessionData gameSessionData)
    {
        LobbyViewData lobbyViewData = new LobbyViewData
        {
            lobbyId = gameSessionData.lobbyId ?? string.Empty,
            playMode = gameSessionData.playMode,
            lobbyName = gameSessionData.lobbyName ?? string.Empty,
            roomCode = gameSessionData.roomCode ?? string.Empty,
            hasPassword = gameSessionData.hasPassword,
            lobbyPassword = gameSessionData.lobbyPassword ?? string.Empty
        };

        customPanelController?.DisplayLobbyInfo(lobbyViewData);
    }

    private void ClearDisplay()
    {
        visiblePlayers.Clear();
        headerController?.ClearHeader();
        playerListController?.DisplayPlayers(visiblePlayers, 0);
        gameInfoController?.ClearInfo();
        customPanelController?.DisplayLobbyInfo(null);
    }

    #endregion

    #region Session Binding

    private IEnumerator BindWhenGameIsReady()
    {
        while (GameSessionManager.instance == null ||
               !GameSessionManager.instance.HasEnteredGame ||
               GameSessionManager.instance.CurrentGameSession == null ||
               GameModeManager.instance == null ||
               !GameModeManager.instance.IsReady)
        {
            yield return null;
        }

        GameSessionManager gameSessionManager = GameSessionManager.instance;

        gameSessionManager.GameSessionUpdated -= OnGameSessionUpdated;
        gameSessionManager.GameSessionUpdated += OnGameSessionUpdated;

        DisplayGameInfo(gameSessionManager.CurrentGameSession);
        bindRoutine = null;
    }

    private void OnGameSessionUpdated(GameSessionData gameSessionData)
    {
        DisplayGameInfo(gameSessionData);
    }

    private void UnsubscribeFromGameSessionManager()
    {
        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.GameSessionUpdated -= OnGameSessionUpdated;
        }
    }

    #endregion

    #region Header

    private void SubscribeToHeader()
    {
        if (headerController == null)
        {
            return;
        }

        headerController.LeaveRequested -= LeaveGame;
        headerController.LeaveRequested += LeaveGame;
    }

    private void UnsubscribeFromHeader()
    {
        if (headerController != null)
        {
            headerController.LeaveRequested -= LeaveGame;
        }
    }

    private void LeaveGame()
    {
        headerController?.SetLeaveInteractable(false);

        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.LeaveCurrentGame();
            return;
        }

        UserManager.instance?.ClearLastGameId();
        GameSceneManager.instance?.LoadMainScene();
    }

    #endregion
}

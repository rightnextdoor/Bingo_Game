using UnityEngine;

[DisallowMultipleComponent]
public class LobbyGameInfoController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Game Info")]
    [SerializeField] private GameInfoController gameInfoController;

    #endregion

    #region Public Methods

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            gameInfoController?.ClearInfo();
            return;
        }

        GameModeManager gameModeManager = GameModeManager.instance;

        string gameName = GetGameName(lobbyViewData, gameModeManager);
        string gameDescription = GetGameDescription(lobbyViewData.gameModeType, gameModeManager);
        string ruleDescription = GetRuleDescription(lobbyViewData, gameModeManager);

        gameInfoController?.ShowGameInfo(
            gameName,
            gameDescription,
            lobbyViewData.ballCountType,
            lobbyViewData.hasRule,
            ruleDescription,
            lobbyViewData.patternTypes);
    }

    public void ClearInfo()
    {
        gameInfoController?.ClearInfo();
    }

    #endregion

    #region Game Info

    private string GetGameName(LobbyViewData lobbyViewData, GameModeManager gameModeManager)
    {
        if (!string.IsNullOrWhiteSpace(lobbyViewData.gameModeName))
        {
            return lobbyViewData.gameModeName;
        }

        BingoGameModeData gameModeData = gameModeManager != null
            ? gameModeManager.GetGameModeData(lobbyViewData.gameModeType)
            : null;

        if (gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.GameName))
        {
            return gameModeData.GameName;
        }

        return lobbyViewData.gameModeType.ToString();
    }

    private string GetGameDescription(BingoGameModeType gameModeType, GameModeManager gameModeManager)
    {
        BingoGameModeData gameModeData = gameModeManager != null
            ? gameModeManager.GetGameModeData(gameModeType)
            : null;

        if (gameModeData == null || string.IsNullOrWhiteSpace(gameModeData.Description))
        {
            return "No game information is available for this game mode.";
        }

        return gameModeData.Description;
    }

    private string GetRuleDescription(LobbyViewData lobbyViewData, GameModeManager gameModeManager)
    {
        if (!lobbyViewData.hasRule)
        {
            return string.Empty;
        }

        BingoGameRuleData ruleData = gameModeManager != null
            ? gameModeManager.GetGameRuleData(lobbyViewData.ruleType)
            : null;

        if (ruleData == null || string.IsNullOrWhiteSpace(ruleData.Description))
        {
            return "No rule description is available for this game mode.";
        }

        return ruleData.Description;
    }

    #endregion
}
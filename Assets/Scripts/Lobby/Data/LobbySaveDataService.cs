using System;
using System.Collections.Generic;

public static class LobbySaveDataService
{
    #region Load

    public static void ApplySavedDataToSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null || lobbySetupData.usesSimulationSettings)
        {
            return;
        }

        LobbyData lobbyData = GetLobbyData();

        if (lobbyData == null)
        {
            return;
        }

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Solo:
                ApplySoloData(lobbyData.soloLobbyData, lobbySetupData.soloSetupData);
                break;

            case MainMenuPlayMode.Custom:
                if (lobbySetupData.customSetupData != null &&
                    lobbySetupData.customSetupData.actionType == CustomLobbyActionType.HostLobby)
                {
                    ApplyCustomData(lobbyData.customLobbyData, lobbySetupData.customSetupData.hostSetupData);
                }
                break;
        }
    }

    #endregion

    #region Save

    public static bool SaveHostSettings(LobbyViewData _lobbyViewData, LobbyHostSettingsData _settingsData)
    {
        if (_lobbyViewData == null || _lobbyViewData.usesSimulationSettings || _settingsData == null ||
            (_lobbyViewData.playMode != MainMenuPlayMode.Solo && _lobbyViewData.playMode != MainMenuPlayMode.Custom))
        {
            return false;
        }

        LobbyData lobbyData = GetLobbyData();

        if (lobbyData == null)
        {
            return false;
        }

        switch (_lobbyViewData.playMode)
        {
            case MainMenuPlayMode.Solo:
                CopyToSoloData(_settingsData, lobbyData.soloLobbyData);
                break;

            case MainMenuPlayMode.Custom:
                CopyToCustomData(_settingsData, lobbyData.customLobbyData);
                break;
        }

        CopyRoomSettings(_lobbyViewData.playMode, _settingsData.maxPlayers, _settingsData.maxPlayer);
        SaveManager.instance?.SaveGame();
        return true;
    }

    public static bool SaveLobbyViewData(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || lobbyViewData.usesSimulationSettings ||
            (lobbyViewData.playMode != MainMenuPlayMode.Solo && lobbyViewData.playMode != MainMenuPlayMode.Custom))
        {
            return false;
        }

        LobbyData lobbyData = GetLobbyData();

        if (lobbyData == null)
        {
            return false;
        }

        switch (lobbyViewData.playMode)
        {
            case MainMenuPlayMode.Solo:
                CopyToSoloData(lobbyViewData, lobbyData.soloLobbyData);
                break;

            case MainMenuPlayMode.Custom:
                CopyToCustomData(lobbyViewData, lobbyData.customLobbyData);
                break;
        }

        CopyRoomSettings(lobbyViewData.playMode, lobbyViewData.maxPlayers, lobbyViewData.maxPlayer);
        SaveManager.instance?.SaveGame();
        return true;
    }

    public static void SaveSoloBotCount(Lobby _lobby)
    {
        SaveManager saveManager = SaveManager.instance;
        LobbyManager lobbyManager = LobbyManager.instance;

        if (_lobby == null || _lobby.usesSimulationSettings || _lobby.playMode != MainMenuPlayMode.Solo ||
            lobbyManager == null || lobbyManager.CurrentLobby != _lobby ||
            saveManager == null || !saveManager.HasLoadedData || saveManager.Data == null)
        {
            return;
        }

        LobbyController controller = _lobby.Controller;

        if (controller.HasPendingWork || controller.IsEmpty || _lobby.lobbyState == LobbyState.Closed)
        {
            return;
        }

        MenuData menuData = GetMenuData();
        int botCount = controller.BotCount;

        if (menuData.soloMenuData.botCount == botCount)
        {
            return;
        }

        menuData.soloMenuData.botCount = botCount;
        saveManager.SaveGame();
    }

    #endregion

    #region Load Helpers

    private static void ApplySoloData(SoloLobbyData savedData, SoloLobbySetupData setupData)
    {
        if (savedData == null || setupData == null)
        {
            return;
        }

        Repair(savedData);

        setupData.gameModeType = savedData.gameModeType;
        setupData.ballCountType = savedData.ballCountType;
        setupData.useFreeCell = savedData.useFreeCell;
        setupData.usesDefaultRank = savedData.usesDefaultRank;
        setupData.useRank = savedData.useRank;
        setupData.hasRiskMatchDurationOverride = savedData.hasRiskMatchDurationOverride;
        setupData.riskMatchDurationMinutes = savedData.riskMatchDurationMinutes;
        setupData.usesDefaultPatterns = savedData.usesDefaultPatterns;
        setupData.patternTypes = CopyPatterns(savedData.patternTypes, savedData.usesDefaultPatterns);
    }

    private static void ApplyCustomData(CustomLobbyData savedData, CustomHostLobbySetupData setupData)
    {
        if (savedData == null || setupData == null)
        {
            return;
        }

        Repair(savedData);

        setupData.gameModeType = savedData.gameModeType;
        setupData.ballCountType = savedData.ballCountType;
        setupData.useFreeCell = savedData.useFreeCell;
        setupData.usesDefaultRank = savedData.usesDefaultRank;
        setupData.useRank = savedData.useRank;
        setupData.hasRiskMatchDurationOverride = savedData.hasRiskMatchDurationOverride;
        setupData.riskMatchDurationMinutes = savedData.riskMatchDurationMinutes;
        setupData.usesDefaultPatterns = savedData.usesDefaultPatterns;
        setupData.patternTypes = CopyPatterns(savedData.patternTypes, savedData.usesDefaultPatterns);
    }

    #endregion

    #region Save Helpers

    private static void CopyRoomSettings(MainMenuPlayMode _playMode, bool _maxPlayers, int _maxPlayer)
    {
        MenuData menuData = GetMenuData();

        if (_playMode == MainMenuPlayMode.Solo)
        {
            menuData.soloMenuData.maxPlayers = _maxPlayers;

            if (!_maxPlayers)
            {
                menuData.soloMenuData.lobbySize = _maxPlayer;
            }
        }
        else if (_playMode == MainMenuPlayMode.Custom)
        {
            menuData.customMenuData.maxPlayers = _maxPlayers;

            if (!_maxPlayers)
            {
                menuData.customMenuData.lobbySize = _maxPlayer;
            }
        }
    }

    private static void CopyToSoloData(LobbyHostSettingsData source, SoloLobbyData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.gameModeType = source.gameModeType;
        target.ballCountType = source.ballCountType;
        target.useFreeCell = source.useFreeCell;
        target.usesDefaultRank = source.usesDefaultRank;
        target.useRank = source.useRank;
        target.hasRiskMatchDurationOverride = source.hasRiskMatchDurationOverride;
        target.riskMatchDurationMinutes = source.riskMatchDurationMinutes;
        target.usesDefaultPatterns = source.usesDefaultPatterns;
        target.patternTypes = CopyPatterns(source.patternTypes, source.usesDefaultPatterns);
    }

    private static void CopyToCustomData(LobbyHostSettingsData source, CustomLobbyData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.gameModeType = source.gameModeType;
        target.ballCountType = source.ballCountType;
        target.useFreeCell = source.useFreeCell;
        target.usesDefaultRank = source.usesDefaultRank;
        target.useRank = source.useRank;
        target.hasRiskMatchDurationOverride = source.hasRiskMatchDurationOverride;
        target.riskMatchDurationMinutes = source.riskMatchDurationMinutes;
        target.usesDefaultPatterns = source.usesDefaultPatterns;
        target.patternTypes = CopyPatterns(source.patternTypes, source.usesDefaultPatterns);
    }

    private static void CopyToSoloData(LobbyViewData source, SoloLobbyData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.gameModeType = source.gameModeType;
        target.ballCountType = source.ballCountType;
        target.useFreeCell = source.useFreeCell;
        target.usesDefaultRank = source.usesDefaultRank;
        target.useRank = source.useRank;
        target.hasRiskMatchDurationOverride = source.hasRiskMatchDurationOverride;
        target.riskMatchDurationMinutes = source.riskMatchDurationMinutes;
        target.usesDefaultPatterns = source.usesDefaultPatterns;
        target.patternTypes = CopyPatterns(source.patternTypes, source.usesDefaultPatterns);
    }

    private static void CopyToCustomData(LobbyViewData source, CustomLobbyData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.gameModeType = source.gameModeType;
        target.ballCountType = source.ballCountType;
        target.useFreeCell = source.useFreeCell;
        target.usesDefaultRank = source.usesDefaultRank;
        target.useRank = source.useRank;
        target.hasRiskMatchDurationOverride = source.hasRiskMatchDurationOverride;
        target.riskMatchDurationMinutes = source.riskMatchDurationMinutes;
        target.usesDefaultPatterns = source.usesDefaultPatterns;
        target.patternTypes = CopyPatterns(source.patternTypes, source.usesDefaultPatterns);
    }

    #endregion

    #region Data Helpers

    private static MenuData GetMenuData()
    {
        GameData gameData = SaveManager.instance.Data;
        gameData.menuData ??= new MenuData();
        gameData.menuData.soloMenuData ??= new SoloMenuData();
        gameData.menuData.customMenuData ??= new CustomMenuData();
        return gameData.menuData;
    }

    private static LobbyData GetLobbyData()
    {
        SaveManager saveManager = SaveManager.instance;

        if (saveManager == null || !saveManager.HasLoadedData || saveManager.Data == null)
        {
            return null;
        }

        GameData gameData = saveManager.Data;

        gameData.lobbyData ??= new LobbyData();
        gameData.lobbyData.soloLobbyData ??= new SoloLobbyData();
        gameData.lobbyData.customLobbyData ??= new CustomLobbyData();

        if (gameData.lobbyData.lobbyVersion < 3)
        {
            gameData.lobbyData.soloLobbyData.usesDefaultRank = true;
            gameData.lobbyData.customLobbyData.usesDefaultRank = true;
            gameData.lobbyData.lobbyVersion = 3;
        }

        Repair(gameData.lobbyData.soloLobbyData);
        Repair(gameData.lobbyData.customLobbyData);

        return gameData.lobbyData;
    }

    private static List<BingoPatternType> CopyPatterns(IReadOnlyList<BingoPatternType> source, bool usesDefaultPatterns)
    {
        List<BingoPatternType> patterns = new List<BingoPatternType>();

        if (usesDefaultPatterns || source == null)
        {
            return patterns;
        }

        for (int i = 0; i < source.Count; i++)
        {
            BingoPatternType patternType = source[i];

            if (!Enum.IsDefined(typeof(BingoPatternType), patternType) || patterns.Contains(patternType))
            {
                continue;
            }

            patterns.Add(patternType);
        }

        return patterns;
    }

    private static void Repair(SoloLobbyData data)
    {
        if (data == null)
        {
            return;
        }

        data.patternTypes ??= new List<BingoPatternType>();
        data.riskMatchDurationMinutes = Math.Max(0f, data.riskMatchDurationMinutes);

        if (!data.usesDefaultPatterns && data.patternTypes.Count == 0)
        {
            data.usesDefaultPatterns = true;
        }
    }

    private static void Repair(CustomLobbyData data)
    {
        if (data == null)
        {
            return;
        }

        data.patternTypes ??= new List<BingoPatternType>();
        data.riskMatchDurationMinutes = Math.Max(0f, data.riskMatchDurationMinutes);

        if (!data.usesDefaultPatterns && data.patternTypes.Count == 0)
        {
            data.usesDefaultPatterns = true;
        }
    }

    #endregion
}

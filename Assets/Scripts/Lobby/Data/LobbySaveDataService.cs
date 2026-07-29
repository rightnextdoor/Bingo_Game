using System;
using System.Collections.Generic;

public static class LobbySaveDataService
{
    #region Load

    public static void ApplySavedDataToSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
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

    public static bool SaveHostSettings(MainMenuPlayMode playMode, LobbyHostSettingsData settingsData)
    {
        if (settingsData == null ||
            (playMode != MainMenuPlayMode.Solo && playMode != MainMenuPlayMode.Custom))
        {
            return false;
        }

        LobbyData lobbyData = GetLobbyData();

        if (lobbyData == null)
        {
            return false;
        }

        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                CopyToSoloData(settingsData, lobbyData.soloLobbyData);
                break;

            case MainMenuPlayMode.Custom:
                CopyToCustomData(settingsData, lobbyData.customLobbyData);
                break;
        }

        SaveManager.instance?.SaveGame();
        return true;
    }

    public static bool SaveLobbyViewData(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null ||
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

        SaveManager.instance?.SaveGame();
        return true;
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
        setupData.usesDefaultPatterns = savedData.usesDefaultPatterns;
        setupData.patternTypes = CopyPatterns(savedData.patternTypes, savedData.usesDefaultPatterns);
    }

    #endregion

    #region Save Helpers

    private static void CopyToSoloData(LobbyHostSettingsData source, SoloLobbyData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.gameModeType = source.gameModeType;
        target.ballCountType = source.ballCountType;
        target.useFreeCell = source.useFreeCell;
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
        target.usesDefaultPatterns = source.usesDefaultPatterns;
        target.patternTypes = CopyPatterns(source.patternTypes, source.usesDefaultPatterns);
    }

    #endregion

    #region Data Helpers

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

        if (!data.usesDefaultPatterns && data.patternTypes.Count == 0)
        {
            data.usesDefaultPatterns = true;
        }
    }

    #endregion
}

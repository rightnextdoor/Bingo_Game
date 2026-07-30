using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalLobbyManager : MonoBehaviour, ILobbyService
{
    #region Fields

    public static LocalLobbyManager instance;

    private const int WorkBatchSize = 10;

    private readonly List<Lobby> lobbies = new List<Lobby>();
    private bool isReady;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Local;
    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (BotManager.instance == null || !BotManager.instance.IsReady)
        {
            yield return null;
        }

        isReady = true;
    }


    private void Update()
    {
        if (!isReady)
        {
            return;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            LobbyController controller = lobbies[i]?.Controller;

            if (controller != null && controller.HasPendingWork)
            {
                controller.ProcessPendingWorkBatch(WorkBatchSize, out _);
            }
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllLobbyControllers();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Lobby Entry

    public Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        return Task.FromResult(EnterLobby(lobbySetupData));
    }

    private LobbyEntryResult EnterLobby(LobbySetupData lobbySetupData)
    {
        if (!isReady)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The local lobby manager is not ready.");
        }

        if (!IsValidSoloSetup(lobbySetupData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The Solo lobby setup data is invalid.");
        }

        UserData userData = lobbySetupData.userData;
        Lobby existingUserLobby = FindUserLobby(userData.userId);

        if (existingUserLobby != null)
        {
            return LobbyEntryResult.SucceededLocal(existingUserLobby);
        }

        Lobby selectedLobby = CreateLobby(lobbySetupData);

        if (selectedLobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyCreationFailed, "The Solo lobby could not be created.");
        }

        if (selectedLobby.Controller.IsFull)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyFull, "The Solo lobby is full.");
        }

        LobbyPlayerData playerData = new LobbyPlayerData(userData, true);

        if (!selectedLobby.Controller.AddPlayer(playerData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The player could not be added to the Solo lobby.");
        }

        selectedLobby.Controller.FillBotsToMinimumPlayers();
        return LobbyEntryResult.SucceededLocal(selectedLobby);
    }

    #endregion

    #region Lobby Exit

    public Task<LobbyExitResult> LeaveLobbyAsync(string userId)
    {
        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return Task.FromResult(LobbyExitResult.Succeeded(userId, LobbyPlayerExitReason.VoluntaryLeave, false, 0, false, LobbyCloseReason.None));
        }

        return Task.FromResult(lobby.Controller.RemovePlayer(userId, LobbyPlayerExitReason.VoluntaryLeave));
    }

    #endregion

    #region Lobby Creation

    private Lobby CreateLobby(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        Lobby lobby = new Lobby(lobbySetupData);
        lobby.Controller.PlayerExitProcessed += OnLobbyPlayerExitProcessed;
        lobby.Controller.SetBotUserProvider(GetLocalBotUsers);
        lobbies.Add(lobby);
        return lobby;
    }

    private void DeleteLobby(Lobby lobby)
    {
        if (lobby == null)
        {
            return;
        }

        if (lobby.Controller != null)
        {
            lobby.Controller.PlayerExitProcessed -= OnLobbyPlayerExitProcessed;
        }

        lobbies.Remove(lobby);
    }

    #endregion

    #region Lobby Lookup

    private Lobby FindUserLobby(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller != null && lobby.Controller.HasPlayer(userId))
            {
                return lobby;
            }
        }

        return null;
    }

    private Lobby FindLobbyByController(LobbyController controller)
    {
        if (controller == null)
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller == controller)
            {
                return lobby;
            }
        }

        return null;
    }

    #endregion

    #region Lobby Events

    private void OnLobbyPlayerExitProcessed(LobbyController controller, LobbyExitResult exitResult)
    {
        if (controller == null || exitResult == null || !exitResult.success || !exitResult.shouldCloseLobby)
        {
            return;
        }

        Lobby lobby = FindLobbyByController(controller);

        if (lobby == null)
        {
            return;
        }

        controller.CloseLobby(exitResult.closeReason);
        DeleteLobby(lobby);
    }

    private void UnsubscribeFromAllLobbyControllers()
    {
        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller != null)
            {
                lobby.Controller.PlayerExitProcessed -= OnLobbyPlayerExitProcessed;
            }
        }
    }

    #endregion

    #region Bots

    private IReadOnlyList<UserData> GetLocalBotUsers()
    {
        return BotManager.instance != null ? BotManager.instance.GetLocalBotUsers() : Array.Empty<UserData>();
    }

    #endregion

    #region Validation

    private bool IsValidSoloSetup(LobbySetupData lobbySetupData)
    {
        return lobbySetupData != null &&
               lobbySetupData.playMode == MainMenuPlayMode.Solo &&
               lobbySetupData.userData != null &&
               lobbySetupData.userData.HasUser &&
               lobbySetupData.soloSetupData != null;
    }

    #endregion
}

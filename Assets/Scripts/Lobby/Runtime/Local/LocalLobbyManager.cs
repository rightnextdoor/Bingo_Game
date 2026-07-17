using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalLobbyManager : MonoBehaviour, ILobbyService
{
    public static LocalLobbyManager instance;

    private readonly List<Lobby> lobbies = new List<Lobby>();

    private bool isReady;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Local;
    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;

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

    private void Start()
    {
        isReady = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllLobbyControllers();

        if (instance == this)
        {
            instance = null;
        }
    }

    public Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        return Task.FromResult(EnterLobby(lobbySetupData));
    }

    public Task<LobbyExitResult> LeaveLobbyAsync(string userId)
    {
        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return Task.FromResult(
                LobbyExitResult.Succeeded(
                    userId,
                    LobbyPlayerExitReason.VoluntaryLeave,
                    false,
                    0,
                    false,
                    LobbyCloseReason.None));
        }

        LobbyExitResult result = lobby.Controller.RemovePlayer(
            userId,
            LobbyPlayerExitReason.VoluntaryLeave);

        return Task.FromResult(result);
    }

    private LobbyEntryResult EnterLobby(LobbySetupData lobbySetupData)
    {
        if (!isReady)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.ServiceUnavailable,
                "The local lobby manager is not ready.");
        }

        if (!IsValidSoloSetup(lobbySetupData))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The Solo lobby setup data is invalid.");
        }

        UserData userData = lobbySetupData.userData;
        Lobby existingUserLobby = FindUserLobby(userData.userId);

        if (existingUserLobby != null)
        {
            return LobbyEntryResult.Succeeded(existingUserLobby);
        }

        Lobby selectedLobby = CreateLobby(lobbySetupData);

        if (selectedLobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyCreationFailed,
                "The Solo lobby could not be created.");
        }

        if (selectedLobby.Controller.IsFull)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyFull,
                "The Solo lobby is full.");
        }

        LobbyPlayerData playerData = new LobbyPlayerData(userData, true);

        if (!selectedLobby.Controller.AddPlayer(playerData))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyJoinFailed,
                "The player could not be added to the Solo lobby.");
        }

        return LobbyEntryResult.Succeeded(selectedLobby);
    }

    private bool IsValidSoloSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null ||
            lobbySetupData.playMode != MainMenuPlayMode.Solo)
        {
            return false;
        }

        if (lobbySetupData.userData == null ||
            !lobbySetupData.userData.HasUser)
        {
            return false;
        }

        return lobbySetupData.soloSetupData != null;
    }

    private Lobby FindUserLobby(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller != null &&
                lobby.Controller.HasPlayer(userId))
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

    private Lobby CreateLobby(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        Lobby lobby = new Lobby(lobbySetupData);
        lobby.Controller.PlayerExitProcessed += OnLobbyPlayerExitProcessed;

        lobbies.Add(lobby);

        return lobby;
    }

    private void OnLobbyPlayerExitProcessed(
        LobbyController controller,
        LobbyExitResult exitResult)
    {
        if (controller == null ||
            exitResult == null ||
            !exitResult.success ||
            !exitResult.shouldCloseLobby)
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
}

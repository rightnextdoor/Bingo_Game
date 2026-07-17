using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalLobbyManager : MonoBehaviour, ILobbyService
{
    public static LocalLobbyManager instance;

    private readonly List<Lobby> lobbies = new List<Lobby>();

    private bool isReady;
    private Lobby currentLobby;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Local;
    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;
    public Lobby CurrentLobby => currentLobby;

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
        if (instance == this)
        {
            instance = null;
        }
    }

    public Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        return Task.FromResult(EnterLobby(lobbySetupData));
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
            currentLobby = existingUserLobby;
            return LobbyEntryResult.Succeeded(existingUserLobby);
        }

        Lobby selectedLobby = CreateLobby(lobbySetupData);

        if (selectedLobby == null || selectedLobby.Controller == null)
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

        currentLobby = selectedLobby;

        return LobbyEntryResult.Succeeded(selectedLobby);
    }

    private bool IsValidSoloSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null || lobbySetupData.playMode != MainMenuPlayMode.Solo)
        {
            return false;
        }

        if (lobbySetupData.userData == null || !lobbySetupData.userData.HasUser)
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

    private Lobby CreateLobby(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        Lobby lobby = new Lobby(lobbySetupData);
        lobbies.Add(lobby);

        return lobby;
    }
}

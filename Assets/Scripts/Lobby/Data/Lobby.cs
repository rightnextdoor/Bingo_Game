using System;
using UnityEngine;

[Serializable]
public class Lobby
{
    [SerializeField] private string lobbyId;
    [SerializeField] private LobbyController controller;

    public MainMenuPlayMode playMode;
    public LobbyState lobbyState;

    public LobbyController Controller
    {
        get
        {
            EnsureController();
            controller.AttachLobby(this);
            return controller;
        }
    }

    public Lobby()
    {
        GenerateLobbyId();

        playMode = MainMenuPlayMode.None;
        lobbyState = LobbyState.Open;

        controller = new LobbyController();
        controller.AttachLobby(this);
    }

    public Lobby(MainMenuPlayMode playMode)
    {
        GenerateLobbyId();

        this.playMode = playMode;
        lobbyState = LobbyState.Open;

        LobbySetupData lobbySetupData = new LobbySetupData
        {
            playMode = playMode
        };

        controller = new LobbyController(this, lobbySetupData, null);
    }

    public Lobby(LobbySetupData lobbySetupData, Func<string, bool> isRoomCodeAvailable = null)
    {
        GenerateLobbyId();

        playMode = lobbySetupData != null
            ? lobbySetupData.playMode
            : MainMenuPlayMode.None;

        lobbyState = LobbyState.Open;

        controller = new LobbyController(this, lobbySetupData, isRoomCodeAvailable);
    }

    public string GetLobbyId()
    {
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            GenerateLobbyId();
        }

        return lobbyId;
    }

    private void GenerateLobbyId()
    {
        lobbyId = Guid.NewGuid().ToString("N");
    }

    private void EnsureController()
    {
        controller ??= new LobbyController();
        controller.AttachLobby(this);
    }

}

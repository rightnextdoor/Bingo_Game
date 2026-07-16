using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Lobby
{
    [SerializeField] private string lobbyId;

    public MainMenuPlayMode playMode;
    public LobbyState lobbyState;

    public List<LobbyPlayerData> players;


    public Lobby()
    {
        GenerateLobbyId();

        playMode = MainMenuPlayMode.None;
        lobbyState = LobbyState.Open;

        players = new List<LobbyPlayerData>();
    }

    public Lobby(MainMenuPlayMode playMode)
    {
        GenerateLobbyId();

        this.playMode = playMode;
        lobbyState = LobbyState.Open;

        players = new List<LobbyPlayerData>();
    }

    public string GetLobbyId()
    {
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            GenerateLobbyId();
        }

        return lobbyId;
    }

    public bool HasPlayer(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        EnsurePlayerList();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData == null ||
                playerData.userData == null)
            {
                continue;
            }

            if (playerData.userData.userId == userId)
            {
                return true;
            }
        }

        return false;
    }

    public LobbyPlayerData GetPlayer(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        EnsurePlayerList();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData == null ||
                playerData.userData == null)
            {
                continue;
            }

            if (playerData.userData.userId == userId)
            {
                return playerData;
            }
        }

        return null;
    }

    public bool AddPlayer(LobbyPlayerData playerData)
    {
        if (playerData == null ||
            !playerData.HasValidUser)
        {
            return false;
        }

        EnsurePlayerList();

        if (HasPlayer(playerData.userData.userId))
        {
            return false;
        }

        players.Add(playerData);

        return true;
    }

    private void GenerateLobbyId()
    {
        lobbyId = Guid.NewGuid().ToString("N");
    }

    private void EnsurePlayerList()
    {
        players ??= new List<LobbyPlayerData>();
    }
}
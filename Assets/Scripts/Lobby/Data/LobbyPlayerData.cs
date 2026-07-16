using System;

[Serializable]
public class LobbyPlayerData
{
    public UserData userData;

    public bool isHost;
    public bool isReady;

    public LobbyBoardData boardData;

    public bool HasValidUser =>
        userData != null &&
        userData.HasUser;


    public LobbyPlayerData()
    {
        userData = new UserData();

        isHost = false;
        isReady = false;

        boardData = new LobbyBoardData();
    }

    public LobbyPlayerData(
        UserData userData,
        bool isHost)
    {
        this.userData = userData ?? new UserData();

        this.isHost = isHost;
        isReady =
            this.userData.userTag == UserTag.Bot;

        boardData = new LobbyBoardData();
    }
}
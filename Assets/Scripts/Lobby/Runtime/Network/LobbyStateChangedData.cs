using System;

[Serializable]
public class LobbyStateChangedData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public LobbyState lobbyState;
    public bool isTimerActive;
    public double timerEndTime;

    #endregion

    #region Constructors

    public LobbyStateChangedData()
    {
        lobbyId = string.Empty;
        revision = 0;
        lobbyState = LobbyState.Open;
        isTimerActive = false;
        timerEndTime = 0d;
    }

    public LobbyStateChangedData(string lobbyId, long revision, LobbyViewData lobbyViewData)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        lobbyState = lobbyViewData != null ? lobbyViewData.lobbyState : LobbyState.Open;
        isTimerActive = lobbyViewData != null && lobbyViewData.isTimerActive;
        timerEndTime = lobbyViewData != null ? lobbyViewData.timerEndTime : 0d;
    }

    #endregion
}

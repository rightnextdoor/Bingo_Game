public enum LobbyState
{
    Open,
    FinalCountdown,
    InGame,
    Closed
}

public enum LobbyEntryState
{
    Idle,
    WaitingForService,
    Connecting,
    Searching,
    Creating,
    Joining,
    AddingPlayer,
    Completed,
    Failed
}

public enum LobbyPlayerExitReason
{
    VoluntaryLeave,
    Kicked,
    Disconnected,
    LobbyStarted,
    JoinTimedOut,
    LobbyClosed
}

public enum LobbyCloseReason
{
    None,
    Empty,
    HostLeft
}

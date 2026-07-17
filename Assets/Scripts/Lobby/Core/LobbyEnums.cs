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
    LobbyClosed
}

public enum LobbyCloseReason
{
    None,
    Empty,
    HostLeft
}

public enum LobbyEntryFailureType
{
    None,
    InvalidSetupData,
    UserMissing,
    ServiceUnavailable,
    NetworkConnectionFailed,
    NetworkLobbyConnectionUnavailable,
    LobbyNotFound,
    LobbyFull,
    InvalidPassword,
    AlreadyInLobby,
    LobbyCreationFailed,
    LobbyJoinFailed,
    LobbyLeaveFailed,
    KickedFromLobby,
    LobbyClosed,
    ConnectionLost,
    Unknown
}

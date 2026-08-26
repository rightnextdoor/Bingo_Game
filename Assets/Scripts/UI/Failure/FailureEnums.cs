public enum FailureDisplayMode
{
    ShowNow,
    WaitForMain
}

public enum FailurePrecedence
{
    None = 0,
    Generic = 50,
    SessionConnection = 100,
    Domain = 200,
    Online = 300
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
    LobbyStarted,
    JoinTimedOut,
    Unknown
}

public enum OnlineFailureType
{
    None,
    ConnectionFailed,
    ConnectionLost,
    Unknown
}

public enum GameFailureType
{
    None,
    ConnectionLost,
    Unknown
}
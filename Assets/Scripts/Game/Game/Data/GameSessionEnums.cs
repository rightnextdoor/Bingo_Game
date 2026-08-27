public enum GameSessionState
{
    Created,
    InProgress,
    Completed
}

public enum GameSessionOperationType
{
    None,
    Create,
    Rejoin
}

public enum GameSessionEntryState
{
    Idle,
    WaitingForService,
    Joining,
    Completed,
    Failed
}

public enum GameSessionFailureType
{
    None,
    InvalidSetupData,
    ServiceUnavailable,
    GameCreationFailed,
    GameNotFound,
    PlayerNotFound,
    PlayerNotEligible,
    NetworkConnectionFailed,
    NetworkGameConnectionUnavailable,
    Unknown
}

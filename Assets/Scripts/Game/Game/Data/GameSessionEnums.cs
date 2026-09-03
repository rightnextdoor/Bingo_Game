public enum GameSessionState
{
    Created,
    InProgress,
    Completed
}

public enum GamePlayPhase
{
    WaitingForFirstPlayer,
    FirstBallCountdown,
    NextBallCountdown,
    Ended
}

public enum GamePlayerStatus
{
    Eligible,
    Won,
    Lost
}

public enum GameEndReason
{
    None,
    RuleCompleted,
    BallPoolExhausted,
    TimerExpired,
    NoEligiblePlayers,
    AuthorityEnded
}

public enum GameSessionOperationType
{
    None,
    Create,
    Rejoin,
    Sync,
    SceneReady,
    Leave
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

public enum ChatConversationType
{
    Session,
    FriendDirect,
    FriendGroup
}

public enum ChatCommandAvailability
{
    All,
    SessionOnly,
    FriendsOnly
}

public enum ChatConnectionState
{
    Disabled,
    Connecting,
    Ready,
    Unavailable
}

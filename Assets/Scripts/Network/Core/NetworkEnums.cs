public enum SessionRuntimeType
{
    Local,
    Network
}

public enum NetworkConnectionMode
{
    Offline,
    DirectHost,
    DirectClient,
    RelayHost,
    RelayClient
}

public enum NetworkConnectionState
{
    Offline,
    Initializing,
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Failed
}

public enum NetworkRelayConnectionType
{
    Dtls,
    Udp,
    Wss
}

public enum OnlineConnectionState
{
    NotStarted,
    Connecting,
    Online,
    Offline
}
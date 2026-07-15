using UnityEngine;

[CreateAssetMenu(
    fileName = "NetworkRuntimeConfigData",
    menuName = "Bingo Game/Network/Network Runtime Config")]
public class NetworkRuntimeConfigData : ScriptableObject
{
    [Header("Protocol")]
    [Min(1)]
    [SerializeField] private int protocolVersion = 1;

    [Header("Direct Connection")]
    [SerializeField] private string defaultDirectAddress = "127.0.0.1";
    [SerializeField] private string defaultListenAddress = "0.0.0.0";

    [Range(1, 65535)]
    [SerializeField] private int defaultPort = 7777;

    [Header("Connection")]
    [Min(1)]
    [SerializeField] private int maximumConnections = 16;

    [Min(1)]
    [SerializeField] private int maximumApprovalPayloadBytes = 512;

    [Min(1)]
    [SerializeField] private int maximumBingoUserIdLength = 64;

    [Header("Relay")]
    [SerializeField]
    private NetworkRelayConnectionType relayConnectionType =
        NetworkRelayConnectionType.Dtls;


    public int ProtocolVersion => protocolVersion;

    public string DefaultDirectAddress => defaultDirectAddress;
    public string DefaultListenAddress => defaultListenAddress;
    public ushort DefaultPort => (ushort)Mathf.Clamp(defaultPort, 1, 65535);

    public int MaximumConnections => Mathf.Max(1, maximumConnections);
    public int MaximumApprovalPayloadBytes => Mathf.Max(1, maximumApprovalPayloadBytes);
    public int MaximumBingoUserIdLength => Mathf.Max(1, maximumBingoUserIdLength);

    public NetworkRelayConnectionType RelayConnectionType => relayConnectionType;

    public int MaximumRelayJoinConnections =>
        Mathf.Max(1, MaximumConnections - 1);


    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(defaultDirectAddress))
        {
            defaultDirectAddress = "127.0.0.1";
        }

        if (string.IsNullOrWhiteSpace(defaultListenAddress))
        {
            defaultListenAddress = "0.0.0.0";
        }

        defaultPort = Mathf.Clamp(defaultPort, 1, 65535);
        maximumConnections = Mathf.Max(1, maximumConnections);
        maximumApprovalPayloadBytes = Mathf.Max(1, maximumApprovalPayloadBytes);
        maximumBingoUserIdLength = Mathf.Max(1, maximumBingoUserIdLength);
    }
}
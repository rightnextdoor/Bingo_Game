using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(NetworkSessionPlayer))]
public class NetworkPlayerIdentity : NetworkBehaviour
{
    #region Fields

    private readonly NetworkVariable<FixedString128Bytes> bingoUserId = new NetworkVariable<FixedString128Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public string BingoUserId => bingoUserId.Value.ToString();
    public ulong ClientId => OwnerClientId;

    #endregion

    #region Network Methods

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        NetworkConnectionRegistry connectionRegistry = NetworkConnectionRegistry.instance;

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            Debug.LogError($"NetworkPlayerIdentity for ClientId {OwnerClientId} could not initialize because NetworkConnectionRegistry is not ready.");
            return;
        }

        if (!connectionRegistry.TryGetBingoUserId(OwnerClientId, out string registeredBingoUserId))
        {
            Debug.LogError($"NetworkPlayerIdentity could not find a registered Bingo UserId for ClientId {OwnerClientId}.");
            return;
        }

        bingoUserId.Value = new FixedString128Bytes(registeredBingoUserId);
    }

    #endregion
}

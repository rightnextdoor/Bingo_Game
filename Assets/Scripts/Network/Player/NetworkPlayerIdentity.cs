using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerIdentity : NetworkBehaviour
{
    private NetworkConnectionRegistry connectionRegistry;

    private readonly NetworkVariable<FixedString128Bytes>
        bingoUserId =
            new NetworkVariable<FixedString128Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

    public string BingoUserId =>
        bingoUserId.Value.ToString();

    public ulong ClientId =>
        OwnerClientId;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        connectionRegistry =
            NetworkConnectionRegistry.instance;

        if (connectionRegistry == null ||
            !connectionRegistry.IsReady)
        {
            Debug.LogError(
                $"NetworkPlayerIdentity for ClientId {OwnerClientId} could not initialize because NetworkConnectionRegistry is not ready.");

            return;
        }

        if (!connectionRegistry.TryGetBingoUserId(
                OwnerClientId,
                out string registeredBingoUserId))
        {
            Debug.LogError(
                $"NetworkPlayerIdentity could not find a registered Bingo UserId for ClientId {OwnerClientId}.");

            return;
        }

        bingoUserId.Value =
            new FixedString128Bytes(
                registeredBingoUserId);
    }
}
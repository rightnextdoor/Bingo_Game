using UnityEngine;

[DisallowMultipleComponent]
public class LobbyFailure : MonoBehaviour
{
    #region Failure Data

    public void GetEntryFailure(LobbyEntryResult result, out string message, out FailurePrecedence precedence)
    {
        LobbyEntryFailureType failureType = result != null ? result.failureType : LobbyEntryFailureType.Unknown;

        message = GetFailureMessage(result);
        precedence = GetFailurePrecedence(failureType);
    }

    public void GetForcedExitFailure(LobbyExitNotification notification, out string message, out FailurePrecedence precedence)
    {
        LobbyEntryFailureType failureType = notification != null ? notification.failureType : LobbyEntryFailureType.Unknown;

        if (notification != null && !string.IsNullOrWhiteSpace(notification.message))
        {
            message = notification.message;
        }
        else
        {
            message = GetFailureMessage(failureType);
        }

        precedence = GetForcedExitPrecedence(failureType);
    }

    #endregion

    #region Messages

    private string GetFailureMessage(LobbyEntryResult result)
    {
        if (result != null && !string.IsNullOrWhiteSpace(result.failureMessage))
        {
            return result.failureMessage;
        }

        LobbyEntryFailureType failureType = result != null ? result.failureType : LobbyEntryFailureType.Unknown;
        return GetFailureMessage(failureType);
    }

    private string GetFailureMessage(LobbyEntryFailureType failureType)
    {
        switch (failureType)
        {
            case LobbyEntryFailureType.InvalidSetupData:
                return "The lobby settings were not valid.";

            case LobbyEntryFailureType.UserMissing:
                return "The current player could not be found.";

            case LobbyEntryFailureType.ServiceUnavailable:
                return "The lobby service is currently unavailable.";

            case LobbyEntryFailureType.NetworkConnectionFailed:
                return "The network connection could not be completed.";

            case LobbyEntryFailureType.NetworkLobbyConnectionUnavailable:
                return "The network lobby connection was not available.";

            case LobbyEntryFailureType.LobbyNotFound:
                return "The lobby could not be found.";

            case LobbyEntryFailureType.LobbyFull:
                return "The lobby is full.";

            case LobbyEntryFailureType.InvalidPassword:
                return "The lobby password is incorrect.";

            case LobbyEntryFailureType.AlreadyInLobby:
                return "The player is already in a lobby.";

            case LobbyEntryFailureType.LobbyCreationFailed:
                return "The lobby could not be created.";

            case LobbyEntryFailureType.LobbyJoinFailed:
                return "The lobby could not be joined.";

            case LobbyEntryFailureType.LobbyLeaveFailed:
                return "The lobby could not be left.";

            case LobbyEntryFailureType.KickedFromLobby:
                return "You were removed from the lobby by the host.";

            case LobbyEntryFailureType.LobbyClosed:
                return "The lobby was closed.";

            case LobbyEntryFailureType.ConnectionLost:
                return "The connection to the lobby was lost.";

            case LobbyEntryFailureType.LobbyStarted:
                return "The lobby has already started.";

            case LobbyEntryFailureType.JoinTimedOut:
                return "The lobby join timed out.";

            default:
                return "The lobby could not be entered.";
        }
    }

    #endregion

    #region Precedence

    private FailurePrecedence GetFailurePrecedence(LobbyEntryFailureType failureType)
    {
        switch (failureType)
        {
            case LobbyEntryFailureType.LobbyCreationFailed:
            case LobbyEntryFailureType.LobbyJoinFailed:
            case LobbyEntryFailureType.LobbyLeaveFailed:
            case LobbyEntryFailureType.Unknown:
            case LobbyEntryFailureType.None:
                return FailurePrecedence.Generic;

            case LobbyEntryFailureType.NetworkConnectionFailed:
            case LobbyEntryFailureType.NetworkLobbyConnectionUnavailable:
                return FailurePrecedence.SessionConnection;

            case LobbyEntryFailureType.InvalidSetupData:
            case LobbyEntryFailureType.UserMissing:
            case LobbyEntryFailureType.ServiceUnavailable:
            case LobbyEntryFailureType.LobbyNotFound:
            case LobbyEntryFailureType.LobbyFull:
            case LobbyEntryFailureType.InvalidPassword:
            case LobbyEntryFailureType.AlreadyInLobby:
            case LobbyEntryFailureType.KickedFromLobby:
            case LobbyEntryFailureType.LobbyClosed:
            case LobbyEntryFailureType.ConnectionLost:
            case LobbyEntryFailureType.LobbyStarted:
            case LobbyEntryFailureType.JoinTimedOut:
            default:
                return FailurePrecedence.Domain;
        }
    }

    private FailurePrecedence GetForcedExitPrecedence(LobbyEntryFailureType failureType)
    {
        switch (failureType)
        {
            case LobbyEntryFailureType.KickedFromLobby:
            case LobbyEntryFailureType.LobbyClosed:
            case LobbyEntryFailureType.ConnectionLost:
            case LobbyEntryFailureType.LobbyStarted:
            case LobbyEntryFailureType.JoinTimedOut:
                return FailurePrecedence.Domain;

            default:
                return GetFailurePrecedence(failureType);
        }
    }

    #endregion
}
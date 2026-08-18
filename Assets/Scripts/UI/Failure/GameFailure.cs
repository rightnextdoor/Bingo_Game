using UnityEngine;

[DisallowMultipleComponent]
public class GameFailure : MonoBehaviour
{
    private FailureManager failureManager;

    private void Awake()
    {
        failureManager =
            GetComponent<FailureManager>();
    }

    public bool ReportFailure(
        GameFailureType failureType,
        FailureDisplayMode displayMode,
        string message = "")
    {
        if (failureType == GameFailureType.None)
        {
            return false;
        }

        if (failureManager == null)
        {
            failureManager =
                FailureManager.instance;
        }

        if (failureManager == null)
        {
            return false;
        }

        string finalMessage =
            !string.IsNullOrWhiteSpace(message)
                ? message
                : GetFailureMessage(failureType);

        return failureManager.ReportFailure(
            finalMessage,
            GetFailurePrecedence(failureType),
            displayMode);
    }

    private FailurePrecedence GetFailurePrecedence(
        GameFailureType failureType)
    {
        switch (failureType)
        {
            case GameFailureType.ConnectionLost:
                return FailurePrecedence.SessionConnection;

            default:
                return FailurePrecedence.Domain;
        }
    }

    private string GetFailureMessage(
        GameFailureType failureType)
    {
        switch (failureType)
        {
            case GameFailureType.ConnectionLost:
                return "The connection to the game was lost.";

            default:
                return "The game could not continue.";
        }
    }
}
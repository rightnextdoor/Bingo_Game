using UnityEngine;

[DisallowMultipleComponent]
public class OnlineFailure : MonoBehaviour
{
    #region Private Fields

    private FailureManager failureManager;
    private GameSceneManager gameSceneManager;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        failureManager = GetComponent<FailureManager>();
    }

    #endregion

    #region Failure

    public void ReportFailure(OnlineFailureType failureType)
    {
        if (failureType == OnlineFailureType.None)
        {
            return;
        }

        if (gameSceneManager == null)
        {
            gameSceneManager = GameSceneManager.instance;
        }

        if (gameSceneManager == null)
        {
            return;
        }

        if (gameSceneManager.CurrentSceneType == GameSceneType.Main)
        {
            return;
        }

        QueueOnlineFailure(failureType);

        NetworkBootstrap.instance?.Shutdown();

        gameSceneManager.ReturnToMainSceneAfterFailure();
    }

    private void QueueOnlineFailure(OnlineFailureType failureType)
    {
        if (failureManager == null)
        {
            failureManager = FailureManager.instance;
        }

        if (failureManager == null)
        {
            return;
        }

        failureManager.ReportFailure(
            GetFailureMessage(failureType),
            FailurePrecedence.Online,
            FailureDisplayMode.WaitForMain);
    }

    #endregion

    #region Messages

    private string GetFailureMessage(OnlineFailureType failureType)
    {
        switch (failureType)
        {
            case OnlineFailureType.ConnectionFailed:
                return "Unable to connect to online services. Check your internet connection and try again.";

            case OnlineFailureType.ConnectionLost:
                return "The connection to online services was lost.";

            default:
                return "An online services error occurred.";
        }
    }

    #endregion
}
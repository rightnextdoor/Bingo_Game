using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class OnlineStatusFooterController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    #endregion

    #region Private Fields

    private OnlineConnectionManager connectionManager;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        BindConnectionManager();
    }

    private void Start()
    {
        BindConnectionManager();
    }

    private void OnDisable()
    {
        UnbindConnectionManager();
    }

    #endregion

    #region Binding

    private void BindConnectionManager()
    {
        OnlineConnectionManager manager = OnlineConnectionManager.instance;

        if (manager == null)
        {
            ApplyState(OnlineConnectionState.NotStarted);
            return;
        }

        if (connectionManager != manager)
        {
            UnbindConnectionManager();

            connectionManager = manager;
            connectionManager.ConnectionStateChanged += OnConnectionStateChanged;
        }

        ApplyState(connectionManager.ConnectionState);
    }

    private void UnbindConnectionManager()
    {
        if (connectionManager != null)
        {
            connectionManager.ConnectionStateChanged -= OnConnectionStateChanged;
        }

        connectionManager = null;
    }

    #endregion

    #region State

    private void OnConnectionStateChanged(OnlineConnectionState state)
    {
        ApplyState(state);
    }

    private void ApplyState(OnlineConnectionState state)
    {
        switch (state)
        {
            case OnlineConnectionState.Connecting:
                SetStatus(true, "Connecting to online services...");
                break;

            case OnlineConnectionState.Online:
                SetStatus(true, "Online");
                break;

            case OnlineConnectionState.Offline:
                SetStatus(true, "Offline");
                break;

            case OnlineConnectionState.NotStarted:
            default:
                SetStatus(false, string.Empty);
                break;
        }
    }

    private void SetStatus(bool visible, string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.gameObject.SetActive(visible);
    }

    #endregion
}
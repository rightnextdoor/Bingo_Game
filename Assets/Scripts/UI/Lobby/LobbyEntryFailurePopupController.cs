using TMPro;
using UnityEngine;

public class LobbyEntryFailurePopupController : MonoBehaviour
{
    private const string DefaultFailureMessage = "The lobby could not be entered.";

    [SerializeField] private TMP_Text failureMessageText;

    public void SetFailureMessage(string message)
    {
        if (failureMessageText == null)
        {
            return;
        }

        failureMessageText.text = string.IsNullOrWhiteSpace(message) ? DefaultFailureMessage : message;
    }
}
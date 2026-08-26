using TMPro;
using UnityEngine;

public class FailurePopupController : MonoBehaviour
{
    private const string DefaultFailureMessage = "An error occurred.";

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
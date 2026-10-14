using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SoloLeavePopupController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private bool isSubmitting;

    private void OnEnable()
    {
        isSubmitting = false;
        SetButtonsInteractable(true);

        if (yesButton != null)
        {
            yesButton.onClick.AddListener(SaveAndLeave);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(LeaveWithoutSaving);
        }
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(SaveAndLeave);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(LeaveWithoutSaving);
        }
    }

    private void SaveAndLeave()
    {
        if (!BeginSubmission())
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSessionManager.instance?.SaveAndLeaveCurrentSoloGame();
    }

    private void LeaveWithoutSaving()
    {
        if (!BeginSubmission())
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSessionManager.instance?.LeaveCurrentSoloWithoutSaving();
    }

    private bool BeginSubmission()
    {
        if (isSubmitting)
        {
            return false;
        }

        isSubmitting = true;
        SetButtonsInteractable(false);
        return true;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (yesButton != null)
        {
            yesButton.interactable = interactable;
        }

        if (noButton != null)
        {
            noButton.interactable = interactable;
        }
    }
}

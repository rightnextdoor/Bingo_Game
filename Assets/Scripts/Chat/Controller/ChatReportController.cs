using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatReportController : MonoBehaviour
{
    [Header("Reported User")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text userIdText;

    [Header("Reason")]
    [SerializeField] private TMP_Dropdown reasonDropdown;

    [Header("Message")]
    [SerializeField] private TMP_InputField reportMessageInput;
    [SerializeField, Min(1)] private int messageCharacterLimit = 500;

    [Header("Block")]
    [SerializeField] private Toggle blockUserToggle;

    [Header("Messages")]
    [SerializeField] private TMP_Text errorText;

    [Header("Bottom Controls")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button sendButton;

    private readonly List<ChatReportReason> reasonOptions = new List<ChatReportReason>();

    private ChatParticipantData reportedParticipant;
    private ChatConversationReference conversation;
    private bool loadingUi;

    public event Action<ChatReportData> ReportSubmitted;

    private void Awake()
    {
        ConfigureInputs();
        BuildReasonOptions();
        ClearRuntimeUi();
    }

    private void OnEnable()
    {
        RegisterListeners();
        ClearRuntimeUi();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public static bool OpenForParticipant(ChatParticipantData participant, ChatConversationReference conversation)
    {
        if (participant == null || !participant.IsValid)
        {
            return false;
        }

        ChatReportController controller = FindControllerIncludingInactive();

        if (controller == null)
        {
            return false;
        }

        if (PopupManager.instance != null)
        {
            PopupManager.instance.OpenPopup(PopupId.ChatReport);

            if (PopupManager.instance.ActivePopupId != PopupId.ChatReport)
            {
                return false;
            }
        }
        else
        {
            controller.gameObject.SetActive(true);
        }

        controller.SetReportedParticipant(participant, conversation);
        return true;
    }

    private static ChatReportController FindControllerIncludingInactive()
    {
        ChatReportController[] controllers = FindObjectsByType<ChatReportController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return controllers != null && controllers.Length > 0 ? controllers[0] : null;
    }

    public void SetReportedParticipant(ChatParticipantData participant, ChatConversationReference newConversation)
    {
        ClearRuntimeUi();

        if (participant == null || !participant.IsValid)
        {
            ShowError("The player to report could not be loaded.");
            return;
        }

        reportedParticipant = participant.Clone();
        conversation = newConversation != null
            ? new ChatConversationReference(newConversation.conversationId, newConversation.conversationType)
            : null;

        if (playerNameText != null)
        {
            playerNameText.text = reportedParticipant.playerName ?? string.Empty;
        }

        if (userIdText != null)
        {
            userIdText.text = reportedParticipant.userId ?? string.Empty;
        }

        bool alreadyBlocked = ChatManager.instance != null && ChatManager.instance.IsUserBlocked(reportedParticipant.userId);

        if (blockUserToggle != null)
        {
            blockUserToggle.SetIsOnWithoutNotify(alreadyBlocked);
            blockUserToggle.interactable = !alreadyBlocked;
        }
    }

    private void ConfigureInputs()
    {
        if (reportMessageInput != null)
        {
            reportMessageInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            reportMessageInput.characterLimit = Mathf.Max(1, messageCharacterLimit);
        }
    }

    private void BuildReasonOptions()
    {
        reasonOptions.Clear();
        reasonOptions.Add(ChatReportReason.None);
        reasonOptions.Add(ChatReportReason.ThreatsOrViolence);
        reasonOptions.Add(ChatReportReason.SexualContentOrBehavior);
        reasonOptions.Add(ChatReportReason.HateOrDiscrimination);
        reasonOptions.Add(ChatReportReason.HarassmentOrBullying);
        reasonOptions.Add(ChatReportReason.Spam);
        reasonOptions.Add(ChatReportReason.Other);

        if (reasonDropdown == null)
        {
            return;
        }

        reasonDropdown.ClearOptions();
        List<string> labels = new List<string>();

        for (int i = 0; i < reasonOptions.Count; i++)
        {
            labels.Add(GetReasonLabel(reasonOptions[i]));
        }

        reasonDropdown.AddOptions(labels);
        reasonDropdown.SetValueWithoutNotify(0);
        reasonDropdown.RefreshShownValue();
    }

    private string GetReasonLabel(ChatReportReason reason)
    {
        switch (reason)
        {
            case ChatReportReason.None:
                return "Select a reason...";
            case ChatReportReason.ThreatsOrViolence:
                return "Threats or Violence";
            case ChatReportReason.SexualContentOrBehavior:
                return "Sexual Content or Behavior";
            case ChatReportReason.HateOrDiscrimination:
                return "Hate or Discrimination";
            case ChatReportReason.HarassmentOrBullying:
                return "Harassment or Bullying";
            case ChatReportReason.Spam:
                return "Spam";
            case ChatReportReason.Other:
                return "Other";
            default:
                return reason.ToString();
        }
    }

    private void RegisterListeners()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendReport);
            sendButton.onClick.AddListener(SendReport);
        }

        if (reasonDropdown != null)
        {
            reasonDropdown.onValueChanged.RemoveListener(OnReasonChanged);
            reasonDropdown.onValueChanged.AddListener(OnReasonChanged);
        }
    }

    private void UnregisterListeners()
    {
        cancelButton?.onClick.RemoveListener(Cancel);
        sendButton?.onClick.RemoveListener(SendReport);
        reasonDropdown?.onValueChanged.RemoveListener(OnReasonChanged);
    }

    private void ClearRuntimeUi()
    {
        loadingUi = true;
        reportedParticipant = null;
        conversation = null;

        if (playerNameText != null)
        {
            playerNameText.text = string.Empty;
        }

        if (userIdText != null)
        {
            userIdText.text = string.Empty;
        }

        if (reasonDropdown != null)
        {
            reasonDropdown.SetValueWithoutNotify(0);
            reasonDropdown.RefreshShownValue();
        }

        if (reportMessageInput != null)
        {
            reportMessageInput.SetTextWithoutNotify(string.Empty);
        }

        if (blockUserToggle != null)
        {
            blockUserToggle.SetIsOnWithoutNotify(false);
            blockUserToggle.interactable = true;
        }

        ClearError();
        loadingUi = false;
    }

    private void OnReasonChanged(int _)
    {
        if (!loadingUi)
        {
            ClearError();
        }
    }

    private void SendReport()
    {
        ClearError();

        if (reportedParticipant == null || !reportedParticipant.IsValid)
        {
            ShowError("The player to report is missing.");
            return;
        }

        ChatReportReason reason = GetSelectedReason();

        if (reason == ChatReportReason.None)
        {
            ShowError("Select a reason for the report.");
            return;
        }

        string reporterUserId = UserManager.instance != null && UserManager.instance.HasUser
            ? UserManager.instance.UserId
            : string.Empty;

        ChatReportData report = new ChatReportData
        {
            reporterUserId = reporterUserId,
            reportedUserId = reportedParticipant.userId,
            reportedPlayerName = reportedParticipant.playerName,
            reason = reason,
            message = reportMessageInput != null ? reportMessageInput.text.Trim() : string.Empty,
            conversationId = conversation?.conversationId ?? string.Empty,
            conversationType = conversation?.conversationType ?? ChatConversationType.Session,
            createdUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        ReportSubmitted?.Invoke(report.Clone());
        Debug.Log($"[ChatReport] {JsonUtility.ToJson(report)}");

        if (blockUserToggle != null && blockUserToggle.isOn && ChatManager.instance != null)
        {
            ChatManager.instance.SetUserBlocked(reportedParticipant.userId, true);
        }

        ClosePopup();
    }

    private ChatReportReason GetSelectedReason()
    {
        if (reasonDropdown == null || reasonOptions.Count == 0)
        {
            return ChatReportReason.None;
        }

        int index = Mathf.Clamp(reasonDropdown.value, 0, reasonOptions.Count - 1);
        return reasonOptions[index];
    }

    private void Cancel()
    {
        ClosePopup();
    }

    private void ClosePopup()
    {
        ClearRuntimeUi();

        if (PopupManager.instance != null)
        {
            PopupManager.instance.CloseActivePopup();
            return;
        }

        gameObject.SetActive(false);
    }

    private void ShowError(string message)
    {
        if (errorText == null)
        {
            return;
        }

        errorText.text = message ?? string.Empty;
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(errorText.text));
    }

    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = string.Empty;
            errorText.gameObject.SetActive(false);
        }
    }
}

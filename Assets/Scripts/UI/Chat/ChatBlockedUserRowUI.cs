using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatBlockedUserRowUI : MonoBehaviour
{
    #region Fields

    [Header("Player")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text userIdText;

    [Header("Controls")]
    [SerializeField] private Button removeButton;

    private string userId = string.Empty;
    private Action<string> removeRequested;

    public string UserId => userId;

    #endregion

    private void Awake()
    {
        Clear();
    }

    private void OnEnable()
    {
        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(OnRemoveClicked);
            removeButton.onClick.AddListener(OnRemoveClicked);
        }
    }

    private void OnDisable()
    {
        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(OnRemoveClicked);
        }
    }

    public void Setup(ChatParticipantData participant, Action<string> onRemoveRequested)
    {
        Clear();

        if (participant == null || !participant.IsValid)
        {
            return;
        }

        userId = participant.userId;
        removeRequested = onRemoveRequested;

        if (playerNameText != null)
        {
            playerNameText.text = participant.playerName;
        }

        if (userIdText != null)
        {
            userIdText.text = participant.userId;
        }

        if (playerIconImage != null)
        {
            Sprite sprite = UIIconManager.instance != null ? UIIconManager.instance.GetPlayerIconSpriteById(participant.iconId) : null;
            playerIconImage.sprite = sprite;
            playerIconImage.enabled = sprite != null;
            playerIconImage.preserveAspect = true;
        }
    }

    public void Clear()
    {
        userId = string.Empty;
        removeRequested = null;

        if (playerNameText != null)
        {
            playerNameText.text = string.Empty;
        }

        if (userIdText != null)
        {
            userIdText.text = string.Empty;
        }

        if (playerIconImage != null)
        {
            playerIconImage.sprite = null;
            playerIconImage.enabled = false;
        }
    }

    private void OnRemoveClicked()
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            removeRequested?.Invoke(userId);
        }
    }
}

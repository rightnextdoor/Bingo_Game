using System;
using System.Collections.Generic;
using UnityEngine;

public enum PopupId
{
    None,
    CreateUser,
    UserInfo,
    Leaderboard,
    Settings,
    LocalPlayOptions,
    OnlineOptions,
    CustomOptions
}

[Serializable]
public class PopupBinding
{
    public PopupId popupId;
    public GameObject popupObject;
}

public class PopupManager : MonoBehaviour
{
    public static PopupManager instance;

    [Header("Overlay")]
    [SerializeField] private GameObject popupOverlay;

    [Header("Popups")]
    [SerializeField] private List<PopupBinding> popups = new();

    public event Action<PopupId> PopupClosed;

    private PopupId activePopupId = PopupId.None;

    public PopupId ActivePopupId => activePopupId;
    public bool HasOpenPopup => activePopupId != PopupId.None;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        CloseAllPopups();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void TogglePopup(PopupId popupId)
    {
        if (popupId == PopupId.None)
        {
            CloseAllPopups();
            return;
        }

        if (activePopupId == popupId)
        {
            CloseActivePopup();
            return;
        }

        OpenPopup(popupId);
    }

    public void OpenPopup(PopupId popupId)
    {
        CloseAllPopups();

        GameObject popup = GetPopupObject(popupId);

        if (popup == null)
        {
            Debug.LogWarning($"PopupManager could not find popup: {popupId}");
            return;
        }

        if (popupOverlay != null)
        {
            popupOverlay.SetActive(true);
        }

        popup.SetActive(true);
        activePopupId = popupId;
    }

    public void CloseActivePopup()
    {
        if (activePopupId == PopupId.None)
        {
            return;
        }

        PopupId closedPopupId = activePopupId;

        GameObject popup = GetPopupObject(activePopupId);

        if (popup != null)
        {
            popup.SetActive(false);
        }

        activePopupId = PopupId.None;

        if (popupOverlay != null)
        {
            popupOverlay.SetActive(false);
        }

        PopupClosed?.Invoke(closedPopupId);
    }

    public void CloseAllPopups()
    {
        PopupId closedPopupId = activePopupId;

        foreach (PopupBinding binding in popups)
        {
            if (binding.popupObject != null)
            {
                binding.popupObject.SetActive(false);
            }
        }

        activePopupId = PopupId.None;

        if (popupOverlay != null)
        {
            popupOverlay.SetActive(false);
        }

        if (closedPopupId != PopupId.None)
        {
            PopupClosed?.Invoke(closedPopupId);
        }
    }

    private GameObject GetPopupObject(PopupId popupId)
    {
        foreach (PopupBinding binding in popups)
        {
            if (binding.popupId == popupId)
            {
                return binding.popupObject;
            }
        }

        return null;
    }
}
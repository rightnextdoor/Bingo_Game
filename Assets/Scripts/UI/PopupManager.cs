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
    CustomOptions,
    LobbyEntryFailure
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
    private PopupId createUserAfterCreatePopupId = PopupId.None;
    private Action createUserAfterCreateAction;

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
        if (popupId == PopupId.CreateUser)
        {
            if (createUserAfterCreatePopupId == PopupId.None && createUserAfterCreateAction == null)
            {
                createUserAfterCreatePopupId = PopupId.UserInfo;
            }
        }
        else
        {
            ClearCreateUserAfterCreateTarget();
        }

        CloseAllPopups(popupId == PopupId.CreateUser);

        GameObject popup = GetPopupObject(popupId);

        if (popup == null)
        {
            if (popupId == PopupId.CreateUser)
            {
                ClearCreateUserAfterCreateTarget();
            }

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

    public void OpenCreateUserPopup(PopupId afterCreatePopupId)
    {
        if (afterCreatePopupId == PopupId.CreateUser)
        {
            Debug.LogWarning("Create User cannot use Create User as its after-create popup. Defaulting to User Info.");
            afterCreatePopupId = PopupId.UserInfo;
        }

        ClearCreateUserAfterCreateTarget();

        createUserAfterCreatePopupId = afterCreatePopupId;
        OpenPopup(PopupId.CreateUser);
    }

    public void OpenCreateUserPopup(Action afterCreateAction)
    {
        ClearCreateUserAfterCreateTarget();

        createUserAfterCreateAction = afterCreateAction;
        OpenPopup(PopupId.CreateUser);
    }

    public void OpenAfterUserCreatedPopup()
    {
        PopupId nextPopupId = createUserAfterCreatePopupId;
        Action nextAction = createUserAfterCreateAction;

        ClearCreateUserAfterCreateTarget();

        CloseActivePopup();

        if (nextAction != null)
        {
            nextAction.Invoke();
            return;
        }

        if (nextPopupId == PopupId.None || nextPopupId == PopupId.CreateUser)
        {
            return;
        }

        OpenPopup(nextPopupId);
    }

    public bool OpenLobbyEntryFailurePopup(string message)
    {
        GameObject popup = GetPopupObject(PopupId.LobbyEntryFailure);

        if (popup == null)
        {
            Debug.LogWarning("PopupManager could not find the Lobby Entry Failure popup.");
            return false;
        }

        LobbyEntryFailurePopupController popupController = popup.GetComponent<LobbyEntryFailurePopupController>();

        if (popupController == null)
        {
            Debug.LogWarning("Lobby Entry Failure popup does not have a LobbyEntryFailurePopupController.");
            return false;
        }

        popupController.SetFailureMessage(message);
        OpenPopup(PopupId.LobbyEntryFailure);

        return activePopupId == PopupId.LobbyEntryFailure;
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

        if (closedPopupId == PopupId.CreateUser)
        {
            ClearCreateUserAfterCreateTarget();
        }

        if (popupOverlay != null)
        {
            popupOverlay.SetActive(false);
        }

        PopupClosed?.Invoke(closedPopupId);
    }

    public void CloseAllPopups()
    {
        CloseAllPopups(false);
    }

    private void CloseAllPopups(bool preserveCreateUserAfterCreateTarget)
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

        if (!preserveCreateUserAfterCreateTarget && closedPopupId == PopupId.CreateUser)
        {
            ClearCreateUserAfterCreateTarget();
        }

        if (popupOverlay != null)
        {
            popupOverlay.SetActive(false);
        }

        if (closedPopupId != PopupId.None)
        {
            PopupClosed?.Invoke(closedPopupId);
        }
    }

    private void ClearCreateUserAfterCreateTarget()
    {
        createUserAfterCreatePopupId = PopupId.None;
        createUserAfterCreateAction = null;
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
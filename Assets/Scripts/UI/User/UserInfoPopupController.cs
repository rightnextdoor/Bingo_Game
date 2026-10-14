using BingoGame.UI.User;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UserInfoPopupController : MonoBehaviour
{
    [Header("Managers")]
    private PopupManager popupManager;
    private UserManager userManager;
    private UIIconManager iconManager;
    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Icon Section")]
    [SerializeField] private Image userIconImage;
    [SerializeField] private Button changeIconButton;

    [Header("Name View Section")]
    [SerializeField] private GameObject nameViewGroup;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private Button updateNameButton;

    [Header("Name Edit Section")]
    [SerializeField] private GameObject nameEditGroup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button saveNameButton;
    [SerializeField] private Button cancelNameButton;
    [SerializeField] private TMP_Text nameErrorText;

    [Header("Stats Section")]
    [Tooltip("Image row prefab displaying a stat name and points.")]
    [SerializeField] private UserStatRowUI statRowPrefab;
    [Tooltip("StatsContent, containing only stat row instances.")]
    [SerializeField] private RectTransform statsContent;
    [Tooltip("Scroll Rect for the enlarged stats list.")]
    [SerializeField] private ScrollRect statsScrollRect;

    [Space]
    [Header("User Scroll")]
    [Tooltip("Outer Scroll Rect containing Icon, Name, and Stats sections.")]
    [SerializeField] private ScrollRect userScrollRect;

    [Space]
    [Header("Stats Filters")]
    [Tooltip("Solo radio toggle in the stats section.")]
    [SerializeField] private Toggle soloStatsToggle;
    [Tooltip("Online radio toggle in the stats section.")]
    [SerializeField] private Toggle onlineStatsToggle;

    private ScorePlayMode statsPlayMode = ScorePlayMode.Solo;

    [Header("Bottom Buttons")]
    [SerializeField] private Button closeButton;

    private const int MinPlayerNameCharacters = 3;
    private const int MaxPlayerNameCharacters = 20;

    private bool moveCaretAfterInitialSelection;
    private readonly List<UserStatRowUI> statRows = new();
    private readonly List<BingoGameModeType> scoredGameModes = new();
    private readonly Vector3[] rowCorners = new Vector3[4];
    private RectTransform statsViewport;
    private RectTransform userViewport;
    private int selectedStatIndex = -1;
    private int heldStatDirection;
    private float nextStatMoveTime;
    private const float StatArrowInitialDelay = 0.35f;
    private const float StatArrowRepeatRate = 0.08f;

    private void Awake()
    {
        foreach (BingoGameModeType gameMode in UserStats.GetScoredGameModes())
        {
            scoredGameModes.Add(gameMode);
        }

        statsViewport = statsScrollRect != null ? statsScrollRect.viewport : null;
        userViewport = userScrollRect != null ? userScrollRect.viewport : null;
    }

    private void Update()
    {
        HandleStatNavigation();
    }

    private void OnEnable()
    {
        CacheManagers();

        SetNameEditMode(false);
        ClearNameError();

        if (changeIconButton != null)
        {
            changeIconButton.onClick.AddListener(OpenIconSelectPopup);
        }

        if (updateNameButton != null)
        {
            updateNameButton.onClick.AddListener(BeginNameEdit);
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.AddListener(SaveNameEdit);
        }

        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.AddListener(CancelNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (nameInputField != null)
        {
            nameInputField.lineType = TMP_InputField.LineType.SingleLine;
            nameInputField.characterLimit = MaxPlayerNameCharacters;
            nameInputField.onValueChanged.AddListener(OnNameInputChanged);
            nameInputField.onSubmit.AddListener(OnNameInputSubmit);
            nameInputField.onEndTextSelection.AddListener(OnNameInputSelectionEnded);
        }

        statsPlayMode = ScorePlayMode.Solo;
        if (soloStatsToggle != null)
        {
            soloStatsToggle.SetIsOnWithoutNotify(true);
            soloStatsToggle.onValueChanged.AddListener(OnSoloStatsChanged);
        }

        if (onlineStatsToggle != null)
        {
            onlineStatsToggle.SetIsOnWithoutNotify(false);
            onlineStatsToggle.onValueChanged.AddListener(OnOnlineStatsChanged);
        }

        ClearStatRows();
        ResetStatArrowHold();
        if (statsScrollRect != null)
        {
            statsScrollRect.StopMovement();
            statsScrollRect.verticalNormalizedPosition = 1f;
        }

        if (userScrollRect != null)
        {
            userScrollRect.StopMovement();
            userScrollRect.verticalNormalizedPosition = 1f;
        }

        UserManager.UserChanged += RefreshDisplay;
        SaveManager.SaveDataChanged += RefreshDisplay;

        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (changeIconButton != null)
        {
            changeIconButton.onClick.RemoveListener(OpenIconSelectPopup);
        }

        if (updateNameButton != null)
        {
            updateNameButton.onClick.RemoveListener(BeginNameEdit);
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.RemoveListener(SaveNameEdit);
        }

        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.RemoveListener(CancelNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }

        if (nameInputField != null)
        {
            nameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
            nameInputField.onSubmit.RemoveListener(OnNameInputSubmit);
            nameInputField.onEndTextSelection.RemoveListener(OnNameInputSelectionEnded);
        }

        if (soloStatsToggle != null)
        {
            soloStatsToggle.onValueChanged.RemoveListener(OnSoloStatsChanged);
        }

        if (onlineStatsToggle != null)
        {
            onlineStatsToggle.onValueChanged.RemoveListener(OnOnlineStatsChanged);
        }

        UserManager.UserChanged -= RefreshDisplay;
        SaveManager.SaveDataChanged -= RefreshDisplay;

        ClearNameError();
        SetNameEditMode(false);
    }

    private void CacheManagers()
    {
        if (popupManager == null)
        {
            popupManager = PopupManager.instance;
        }

        if (userManager == null)
        {
            userManager = UserManager.instance;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }
    }

    public void OpenIconSelectPopup()
    {
        ClearNameError();

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        if (iconSelectPopupController == null)
        {
            ShowNameError("Icon select popup was not found.");
            Debug.LogWarning("UserInfoPopupController needs IconSelectPopupController assigned.");
            return;
        }

        iconSelectPopupController.OpenForSelection(userManager.CurrentUser.iconId, OnIconSelected);
    }

    private void OnIconSelected(UserIconData iconData)
    {
        if (iconData == null)
        {
            return;
        }

        userManager.ChangeIcon(iconData.IconId);

        RefreshDisplay();
    }

    private void BeginNameEdit()
    {
        ClearNameError();

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        if (nameInputField != null)
        {
            nameInputField.text = userManager.CurrentUser.playerName;
        }

        SetNameEditMode(true);

        if (nameInputField != null)
        {
            nameInputField.onFocusSelectAll = true;
            moveCaretAfterInitialSelection = true;
            nameInputField.ActivateInputField();
        }
    }

    private void OnNameInputSelectionEnded(string _text, int _anchorPosition, int _focusPosition)
    {
        if (!moveCaretAfterInitialSelection || nameInputField == null || !nameInputField.isFocused)
        {
            return;
        }

        moveCaretAfterInitialSelection = false;
        int endPosition = nameInputField.text.Length;

        nameInputField.selectionStringAnchorPosition = endPosition;
        nameInputField.selectionStringFocusPosition = endPosition;
    }

    private void SaveNameEdit()
    {
        ClearNameError();

        if (userManager == null || !userManager.HasUser)
        {
            ShowNameError("No user is loaded.");
            return;
        }

        string newPlayerName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;

        if (!IsValidPlayerName(newPlayerName))
        {
            ShowNameError(GetPlayerNameErrorMessage());
            return;
        }

        userManager.ChangePlayerName(newPlayerName);

        SetNameEditMode(false);
        RefreshDisplay();
    }

    private void CancelNameEdit()
    {
        ClearNameError();
        SetNameEditMode(false);
        RefreshDisplay();
    }

    private void OnNameInputChanged(string value)
    {
        moveCaretAfterInitialSelection = false;
        ClearNameError();
    }

    private void OnNameInputSubmit(string value)
    {
        if (nameEditGroup != null && nameEditGroup.activeSelf)
        {
            SaveNameEdit();
        }
    }

    private void RefreshDisplay()
    {
        CacheManagers();

        if (userManager == null || !userManager.HasUser)
        {
            if (playerNameText != null)
            {
                playerNameText.text = "No user saved";
            }

            if (playerIdText != null)
            {
                playerIdText.text = string.Empty;
            }

            ClearStatRows();

            if (userIconImage != null)
            {
                userIconImage.sprite = null;
                userIconImage.enabled = false;
            }

            return;
        }

        UserData userData = userManager.CurrentUser;

        if (playerNameText != null)
        {
            playerNameText.text = userData.playerName;
        }

        if (playerIdText != null)
        {
            playerIdText.text = $"ID: {userData.userId}";
        }

        RefreshStatRows(userData.stats);

        RefreshUserIcon(userData.iconId);
    }

    private void ClearStatRows()
    {
        statRows.Clear();
        selectedStatIndex = -1;
        if (statsContent == null)
        {
            return;
        }

        for (int i = statsContent.childCount - 1; i >= 0; i--)
        {
            GameObject row = statsContent.GetChild(i).gameObject;
            row.SetActive(false);
            Destroy(row);
        }
    }

    private void RefreshStatRows(UserStats _stats)
    {
        if (_stats == null)
        {
            ClearStatRows();
            return;
        }

        if (statRowPrefab == null || statsContent == null)
        {
            return;
        }

        if (statRows.Count == 0)
        {
            CreateStatRow("Overall", _stats.GetOverallPoints(statsPlayMode));
            for (int i = 0; i < scoredGameModes.Count; i++)
            {
                BingoGameModeType gameMode = scoredGameModes[i];
                string name = GameModeManager.instance != null
                    ? GameModeManager.instance.GetGameModeName(gameMode)
                    : gameMode.ToString();
                CreateStatRow(name, _stats.GetPoints(statsPlayMode, gameMode));
            }
        }
        else
        {
            statRows[0].SetPoints(_stats.GetOverallPoints(statsPlayMode));
            for (int i = 0; i < scoredGameModes.Count; i++)
            {
                statRows[i + 1].SetPoints(_stats.GetPoints(statsPlayMode, scoredGameModes[i]));
            }
        }
    }

    private void CreateStatRow(string _name, int _points)
    {
        UserStatRowUI row = Instantiate(statRowPrefab, statsContent);
        row.gameObject.SetActive(true);
        row.Setup(_name, _points, OnStatRowClicked);
        row.SetHighlighted(false);
        statRows.Add(row);
    }

    private void OnStatRowClicked(UserStatRowUI _row)
    {
        int index = statRows.IndexOf(_row);
        if (index < 0)
        {
            return;
        }

        if (index == selectedStatIndex)
        {
            selectedStatIndex = -1;
            _row.SetHighlighted(false);
            ResetStatArrowHold();
            return;
        }

        SelectStatRow(index);
    }

    private void SelectStatRow(int _index)
    {
        if (_index < 0 || _index >= statRows.Count)
        {
            return;
        }

        selectedStatIndex = _index;
        for (int i = 0; i < statRows.Count; i++)
        {
            statRows[i].SetHighlighted(i == selectedStatIndex);
        }

        Canvas.ForceUpdateCanvases();
        RectTransform row = statRows[selectedStatIndex].RectTransform;
        ScrollStatIntoView(statsScrollRect, statsViewport, row, true);
        ScrollStatIntoView(userScrollRect, userViewport, row, false);
    }

    private void HandleStatNavigation()
    {
        if (selectedStatIndex < 0 || selectedStatIndex >= statRows.Count || Keyboard.current == null ||
            (nameInputField != null && nameInputField.isFocused) ||
            (PopupManager.instance != null && PopupManager.instance.ActivePopupId != PopupId.UserInfo))
        {
            ResetStatArrowHold();
            return;
        }

        int direction = 0;
        bool pressed = false;
        if (Keyboard.current.upArrowKey.isPressed)
        {
            direction = -1;
            pressed = Keyboard.current.upArrowKey.wasPressedThisFrame;
        }
        else if (Keyboard.current.downArrowKey.isPressed)
        {
            direction = 1;
            pressed = Keyboard.current.downArrowKey.wasPressedThisFrame;
        }

        if (direction == 0)
        {
            ResetStatArrowHold();
            return;
        }

        if (pressed || heldStatDirection != direction)
        {
            heldStatDirection = direction;
            nextStatMoveTime = Time.unscaledTime + StatArrowInitialDelay;
        }
        else if (Time.unscaledTime < nextStatMoveTime)
        {
            return;
        }
        else
        {
            nextStatMoveTime = Time.unscaledTime + StatArrowRepeatRate;
        }

        int nextIndex = Mathf.Clamp(selectedStatIndex + direction, 0, statRows.Count - 1);
        if (nextIndex != selectedStatIndex)
        {
            SelectStatRow(nextIndex);
        }
    }

    private void ResetStatArrowHold()
    {
        heldStatDirection = 0;
        nextStatMoveTime = 0f;
    }

    private void ScrollStatIntoView(ScrollRect _scrollRect, RectTransform _viewport, RectTransform _row, bool _snapListEnds)
    {
        if (_scrollRect == null || _scrollRect.content == null || _viewport == null || _row == null)
        {
            return;
        }

        if (_snapListEnds && (selectedStatIndex == 0 || selectedStatIndex == statRows.Count - 1))
        {
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = selectedStatIndex == 0 ? 1f : 0f;
            return;
        }

        _row.GetWorldCorners(rowCorners);
        float bottom = _viewport.InverseTransformPoint(rowCorners[0]).y;
        float top = _viewport.InverseTransformPoint(rowCorners[1]).y;
        float delta = 0f;
        if (top > _viewport.rect.yMax || top - bottom > _viewport.rect.height)
        {
            delta = _viewport.rect.yMax - top;
        }
        else if (bottom < _viewport.rect.yMin)
        {
            delta = _viewport.rect.yMin - bottom;
        }

        RectTransform content = _scrollRect.content;
        float maxY = Mathf.Max(0f, content.rect.height - _viewport.rect.height);
        Vector2 position = content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + delta, 0f, maxY);
        content.anchoredPosition = position;
        _scrollRect.StopMovement();
    }

    private void RefreshUserIcon(string iconId)
    {
        if (userIconImage == null)
        {
            return;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        Sprite iconSprite = iconManager != null
            ? iconManager.GetPlayerIconSpriteById(iconId)
            : null;

        userIconImage.sprite = iconSprite;
        userIconImage.enabled = iconSprite != null;
        userIconImage.preserveAspect = true;
    }

    private void OnSoloStatsChanged(bool _isOn)
    {
        if (_isOn)
        {
            statsPlayMode = ScorePlayMode.Solo;
            RefreshDisplay();
        }
    }

    private void OnOnlineStatsChanged(bool _isOn)
    {
        if (_isOn)
        {
            statsPlayMode = ScorePlayMode.Online;
            RefreshDisplay();
        }
    }

    private void SetNameEditMode(bool editing)
    {
        if (!editing)
        {
            moveCaretAfterInitialSelection = false;
        }

        if (nameViewGroup != null)
        {
            nameViewGroup.SetActive(!editing);
        }
        else
        {
            if (playerNameText != null)
            {
                playerNameText.gameObject.SetActive(!editing);
            }

            if (playerIdText != null)
            {
                playerIdText.gameObject.SetActive(!editing);
            }

            if (updateNameButton != null)
            {
                updateNameButton.gameObject.SetActive(!editing);
            }
        }

        if (nameEditGroup != null)
        {
            nameEditGroup.SetActive(editing);
        }
    }

    private bool IsValidPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return false;
        }

        playerName = playerName.Trim();

        return playerName.Length >= MinPlayerNameCharacters &&
               playerName.Length <= MaxPlayerNameCharacters;
    }

    private string GetPlayerNameErrorMessage()
    {
        return $"Player name must be {MinPlayerNameCharacters}-{MaxPlayerNameCharacters} characters.";
    }

    private void ShowNameError(string message)
    {
        if (nameErrorText == null)
        {
            return;
        }

        nameErrorText.text = message;
        nameErrorText.gameObject.SetActive(true);
    }

    private void ClearNameError()
    {
        if (nameErrorText == null)
        {
            return;
        }

        nameErrorText.text = string.Empty;
        nameErrorText.gameObject.SetActive(false);
    }

    private void ClosePopup()
    {
        ClearNameError();
        SetNameEditMode(false);

        if (popupManager == null)
        {
            popupManager = PopupManager.instance;
        }

        if (popupManager != null)
        {
            popupManager.CloseActivePopup();
        }
    }
}

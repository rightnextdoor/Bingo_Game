using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatTabController : MonoBehaviour, IUIThemeTarget
{
    [Header("Tabs")]
    [SerializeField] private Button lobbyTabButton;
    [SerializeField] private UIThemeButton lobbyTabTheme;
    [SerializeField] private Button friendsTabButton;
    [SerializeField] private UIThemeButton friendsTabTheme;

    [Header("Content")]
    [SerializeField] private GameObject lobbyChatContent;
    [SerializeField] private GameObject friendsContent;

    private UIThemeManager themeManager;
    private ChatTabType selectedTab = ChatTabType.Session;

    public ChatTabType SelectedTab => selectedTab;
    public event Action<ChatTabType> TabChanged;

    private void Awake()
    {
        ClearContentState();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RegisterWithThemeManager();
        ApplySelectedTab(false);
    }

    private void OnDisable()
    {
        UnregisterListeners();
        UnregisterFromThemeManager();
    }

    public void SetSelectedTab(ChatTabType tab, bool notify = true)
    {
        if (!Enum.IsDefined(typeof(ChatTabType), tab))
        {
            tab = ChatTabType.Session;
        }

        bool changed = selectedTab != tab;
        selectedTab = tab;
        ApplySelectedTab(notify && changed);
    }

    private void RegisterListeners()
    {
        if (lobbyTabButton != null)
        {
            lobbyTabButton.onClick.RemoveListener(SelectLobbyTab);
            lobbyTabButton.onClick.AddListener(SelectLobbyTab);
        }

        if (friendsTabButton != null)
        {
            friendsTabButton.onClick.RemoveListener(SelectFriendsTab);
            friendsTabButton.onClick.AddListener(SelectFriendsTab);
        }
    }

    private void UnregisterListeners()
    {
        if (lobbyTabButton != null)
        {
            lobbyTabButton.onClick.RemoveListener(SelectLobbyTab);
        }

        if (friendsTabButton != null)
        {
            friendsTabButton.onClick.RemoveListener(SelectFriendsTab);
        }
    }

    private void SelectLobbyTab()
    {
        SetSelectedTab(ChatTabType.Session);
    }

    private void SelectFriendsTab()
    {
        SetSelectedTab(ChatTabType.Friends);
    }

    private void ApplySelectedTab(bool notify)
    {
        bool showLobby = selectedTab == ChatTabType.Session;

        if (lobbyChatContent != null)
        {
            lobbyChatContent.SetActive(showLobby);
        }

        if (friendsContent != null)
        {
            friendsContent.SetActive(!showLobby);
        }

        ReapplyTheme();

        if (notify)
        {
            TabChanged?.Invoke(selectedTab);
        }
    }

    private void ClearContentState()
    {
        if (lobbyChatContent != null)
        {
            lobbyChatContent.SetActive(false);
        }

        if (friendsContent != null)
        {
            friendsContent.SetActive(false);
        }
    }

    private void RegisterWithThemeManager()
    {
        themeManager ??= UIThemeManager.instance;
        themeManager?.Register(this);
    }

    private void UnregisterFromThemeManager()
    {
        if (themeManager != null)
        {
            themeManager.Unregister(this);
            themeManager = null;
        }
    }

    public void ReapplyTheme()
    {
        lobbyTabTheme?.ReapplyTheme();
        friendsTabTheme?.ReapplyTheme();

        themeManager ??= UIThemeManager.instance;

        if (themeManager == null)
        {
            return;
        }

        UIThemeStyle style = themeManager.ApplyTheme(UIThemeSectionType.Button, UIThemeButtonType.ChatTab) as UIThemeStyle;

        if (style == null)
        {
            return;
        }

        ApplyPersistentSelectedColor(lobbyTabButton, selectedTab == ChatTabType.Session, style);
        ApplyPersistentSelectedColor(friendsTabButton, selectedTab == ChatTabType.Friends, style);
    }

    private void ApplyPersistentSelectedColor(Button button, bool selected, UIThemeStyle style)
    {
        if (button == null || style == null)
        {
            return;
        }

        ColorBlock colors = button.colors;

        if (selected)
        {
            colors.normalColor = style.SelectedColor;
            colors.highlightedColor = style.SelectedColor;
            colors.pressedColor = style.PressedColor;
            colors.selectedColor = style.SelectedColor;
        }
        else
        {
            colors.normalColor = style.NormalColor;
            colors.highlightedColor = style.HighlightedColor;
            colors.pressedColor = style.PressedColor;
            colors.selectedColor = style.SelectedColor;
        }

        colors.disabledColor = style.DisabledColor;
        colors.colorMultiplier = style.ColorMultiplier;
        colors.fadeDuration = style.FadeDuration;
        button.colors = colors;
    }
}

using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySceneController : MonoBehaviour, ILobbyView
{
    [Header("Lobby Header")]
    [SerializeField] private TMP_Text titleText;

    [Header("Lobby Identity")]
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playModeText;
    [SerializeField] private TMP_Text lobbyStateText;
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text passwordText;

    [Header("Game Information")]
    [SerializeField] private TMP_Text gameModeText;
    [SerializeField] private TMP_Text ruleText;
    [SerializeField] private TMP_Text patternsText;
    [SerializeField] private TMP_Text ballCountText;

    [Header("Players")]
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text playerListText;

    private LobbyController lobbyController;
    private Coroutine bindRoutine;

    private void OnEnable()
    {
        bindRoutine = StartCoroutine(BindWhenLobbyIsReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnbindLobbyController();
    }

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        SetText(
            titleText,
            $"{lobbyViewData.runtimeType} {lobbyViewData.playMode}");

        SetText(
            lobbyIdText,
            $"Lobby ID: {GetDisplayValue(lobbyViewData.lobbyId)}");

        SetText(
            playModeText,
            $"Play Mode: {lobbyViewData.playMode}");

        SetText(
            lobbyStateText,
            $"Lobby State: {lobbyViewData.lobbyState}");

        SetText(
            lobbyNameText,
            $"Lobby Name: {GetDisplayValue(lobbyViewData.lobbyName)}");

        SetText(
            roomCodeText,
            $"Room Code: {GetDisplayValue(lobbyViewData.roomCode)}");

        SetText(
            passwordText,
            $"Password: {(lobbyViewData.hasPassword ? "Set" : "None")}");

        SetText(
            gameModeText,
            $"Game Mode: {GetDisplayValue(lobbyViewData.gameModeName)}");

        SetText(
            ruleText,
            $"Rule: {(lobbyViewData.hasRule ? lobbyViewData.ruleType.ToString() : "None")}");

        SetText(
            patternsText,
            $"Patterns: {BuildPatternText(lobbyViewData)}");

        SetText(
            ballCountText,
            $"Ball Count: {(int)lobbyViewData.ballCountType}");

        SetText(
            playerCountText,
            $"Players: {lobbyViewData.playerCount} / {lobbyViewData.maxPlayers}");

        SetText(
            playerListText,
            BuildPlayerListText(lobbyViewData));
    }

    private IEnumerator BindWhenLobbyIsReady()
    {
        while (LobbyManager.instance == null ||
               !LobbyManager.instance.HasEnteredLobby ||
               LobbyManager.instance.CurrentLobby == null ||
               LobbyManager.instance.CurrentLobby.Controller == null)
        {
            yield return null;
        }

        BindLobbyController(
            LobbyManager.instance.CurrentLobby.Controller);

        bindRoutine = null;
    }

    private void BindLobbyController(LobbyController controller)
    {
        if (lobbyController == controller)
        {
            lobbyController.RefreshViews();
            return;
        }

        UnbindLobbyController();

        lobbyController = controller;
        lobbyController?.BindView(this);
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null)
        {
            return;
        }

        lobbyController.UnbindView(this);
        lobbyController = null;
    }

    private string BuildPatternText(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData.patternTypes == null ||
            lobbyViewData.patternTypes.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < lobbyViewData.patternTypes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(lobbyViewData.patternTypes[i]);
        }

        return builder.ToString();
    }

    private string BuildPlayerListText(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData.playerNames == null ||
            lobbyViewData.playerNames.Count == 0)
        {
            return "Players:\nNone";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Players:");

        for (int i = 0; i < lobbyViewData.playerNames.Count; i++)
        {
            builder.Append(lobbyViewData.playerNames[i]);

            if (i < lobbyViewData.playerNames.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private string GetDisplayValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "None"
            : value;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    public void LeaveLobby()
    {
        GameSceneManager gameSceneManager = GameSceneManager.instance;

        if (gameSceneManager == null)
        {
            Debug.LogWarning(
                "[LobbySceneController] Could not leave the Lobby because GameSceneManager was not found.");

            return;
        }

        if (gameSceneManager.IsLoadingScene)
        {
            return;
        }

        gameSceneManager.LoadMainScene();
    }
}

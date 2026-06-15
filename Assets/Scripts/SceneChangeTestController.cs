using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SceneChangeTestController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Buttons")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button secondaryButton;

    [Header("Optional Button Labels")]
    [SerializeField] private TMP_Text primaryButtonText;
    [SerializeField] private TMP_Text secondaryButtonText;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        BindButtons();
    }

    private void OnEnable()
    {
        RefreshButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    #endregion

    #region Button Binding

    private void BindButtons()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveListener(OnPrimaryButtonClicked);
            primaryButton.onClick.AddListener(OnPrimaryButtonClicked);
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.RemoveListener(OnSecondaryButtonClicked);
            secondaryButton.onClick.AddListener(OnSecondaryButtonClicked);
        }
    }

    private void UnbindButtons()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveListener(OnPrimaryButtonClicked);
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.RemoveListener(OnSecondaryButtonClicked);
        }
    }

    #endregion

    #region Button Visibility

    private void RefreshButtons()
    {
        GameSceneType sceneType = GetCurrentSceneType();

        switch (sceneType)
        {
            case GameSceneType.Main:
                SetButton(primaryButton, primaryButtonText, true, "Go To Lobby");
                SetButton(secondaryButton, secondaryButtonText, false, "");
                break;

            case GameSceneType.Lobby:
                SetButton(primaryButton, primaryButtonText, true, "Go To Game");
                SetButton(secondaryButton, secondaryButtonText, false, "");
                break;

            case GameSceneType.Game:
                SetButton(primaryButton, primaryButtonText, true, "Go To Main");
                SetButton(secondaryButton, secondaryButtonText, true, "Go To Lobby");
                break;
        }
    }

    private void SetButton(Button button, TMP_Text label, bool isVisible, string labelText)
    {
        if (button != null)
        {
            button.gameObject.SetActive(isVisible);
        }

        if (label != null)
        {
            label.text = labelText;
        }
    }

    #endregion

    #region Button Clicks

    public void OnPrimaryButtonClicked()
    {
        if (GameSceneManager.instance == null || GameSceneManager.instance.IsLoadingScene)
        {
            return;
        }

        AudioManager.instance.PlaySFX("Button");

        GameSceneType sceneType = GetCurrentSceneType();

        switch (sceneType)
        {
            case GameSceneType.Main:
                GameSceneManager.instance.LoadLobbyScene();
                break;

            case GameSceneType.Lobby:
                GameSceneManager.instance.LoadGameScene();
                break;

            case GameSceneType.Game:
                GameSceneManager.instance.LoadMainScene();
                break;
        }
    }

    public void OnSecondaryButtonClicked()
    {
        if (GameSceneManager.instance == null || GameSceneManager.instance.IsLoadingScene)
        {
            return;
        }

        GameSceneType sceneType = GetCurrentSceneType();

        if (sceneType == GameSceneType.Game)
        {
            GameSceneManager.instance.LoadLobbyScene();
        }
    }

    #endregion

    #region Helpers

    private GameSceneType GetCurrentSceneType()
    {
        if (GameSceneManager.instance != null)
        {
            return GameSceneManager.instance.CurrentSceneType;
        }

        return GameSceneType.Main;
    }

    #endregion
}

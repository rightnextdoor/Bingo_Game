using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameRejoinController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private void OnEnable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(RejoinLastGame);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(DeclineLastGame);
        }
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(RejoinLastGame);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(DeclineLastGame);
        }
    }

    private void RejoinLastGame()
    {
        UserData userData = UserManager.instance?.CurrentUser;
        string lastGameId = userData?.lastGameId;

        if (string.IsNullOrWhiteSpace(lastGameId) ||
            GameSessionManager.instance == null ||
            GameSceneManager.instance == null)
        {
            return;
        }

        if (!GameSessionManager.instance.PrepareLastGameRejoin(lastGameId))
        {
            return;
        }

        PopupManager.instance?.CloseActivePopup();
        GameSceneManager.instance.LoadGameScene();
        GameSessionManager.instance.BeginPendingGameEntry();
    }

    private void DeclineLastGame()
    {
        GameSessionManager.instance?.ClearCurrentGame(false);
        UserManager.instance?.ClearLastGameId();
        PopupManager.instance?.CloseActivePopup();
    }
}

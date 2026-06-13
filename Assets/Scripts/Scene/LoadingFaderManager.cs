using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingFaderManager : MonoBehaviour
{
    public static LoadingFaderManager instance;

    #region Inspector Fields

    [Header("Overlay")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private TMP_Text loadingText;

    [Header("Text")]
    [SerializeField] private string loadingTextValue = "Loading...";

    [Header("Timing")]
    [SerializeField] private float minimumShowTime = 2f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private bool hideOnAwake = true;

    #endregion

    #region Private Fields

    private float loadingStartTime;

    #endregion

    #region Properties

    public bool HasMinimumShowTimePassed
    {
        get
        {
            return Time.unscaledTime - loadingStartTime >= minimumShowTime;
        }
    }

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();

        if (hideOnAwake)
        {
            HideInstant();
        }
    }

    #endregion

    #region Loading Display

    public void ShowLoading()
    {
        ResolveReferences();

        loadingStartTime = Time.unscaledTime;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }

        if (loadingText != null)
        {
            loadingText.text = string.IsNullOrWhiteSpace(loadingTextValue)
                ? "Loading..."
                : loadingTextValue;
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
        }
    }

    public IEnumerator FadeOut()
    {
        ResolveReferences();

        if (overlayCanvasGroup == null)
        {
            yield break;
        }

        float timer = 0f;
        float startAlpha = overlayCanvasGroup.alpha;

        while (timer < fadeOutTime)
        {
            timer += Time.unscaledDeltaTime;

            float percent = fadeOutTime <= 0f ? 1f : timer / fadeOutTime;
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, percent);

            yield return null;
        }

        HideInstant();
    }

    public void HideInstant()
    {
        ResolveReferences();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (overlayRoot == null)
        {
            overlayRoot = gameObject;
        }

        if (overlayCanvasGroup == null)
        {
            overlayCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }
    }

    #endregion
}
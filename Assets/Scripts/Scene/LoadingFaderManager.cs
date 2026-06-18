using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingFaderManager : MonoBehaviour
{
    public static LoadingFaderManager instance;

    #region Inspector Fields

    [Header("Overlay")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject loadingLogoArea;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private GameObject loadingBallRenderWorld;

    [Header("Overlay Sorting")]
    [SerializeField] private bool forceOverlayToTop = true;
    [SerializeField] private int overlaySortingOrder = 5000;

    [Header("Text")]
    [SerializeField] private string loadingTextValue = "Loading";

    [Header("Timing")]
    [SerializeField] private float minimumShowTime = 7f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private bool hideOnAwake = true;

    #endregion

    #region Private Fields

    private float loadingStartTime;
    private bool isShowing;
    private bool hasAppliedFadeStartAudio;

    #endregion

    #region Properties

    public bool IsShowing => isShowing;

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
        isShowing = true;
        hasAppliedFadeStartAudio = false;

        SetOverlayObjectsActive(true);
        SetLoadingLogoActive(true);
        ForceOverlayCanvasToTop();
        SetLoadingText();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
        }

        Canvas.ForceUpdateCanvases();
    }

    public IEnumerator FadeOut()
    {
        ResolveReferences();

        while (!HasMinimumShowTimePassed)
        {
            yield return null;
        }

        ApplyFadeStartAudio();
        SetLoadingLogoActive(false);

        if (overlayCanvasGroup == null)
        {
            HideInstant();
            yield break;
        }

        float timer = 0f;
        float startAlpha = overlayCanvasGroup.alpha;
        float duration = Mathf.Max(0.01f, fadeOutTime);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float percent = Mathf.Clamp01(timer / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, percent);

            yield return null;
        }

        HideInstant();
    }

    public void HideInstant()
    {
        ResolveReferences();

        isShowing = false;
        SetLoadingLogoActive(false);

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;
        }

        SetOverlayObjectsActive(false);
    }

    #endregion

    #region Setup

    private void SetOverlayObjectsActive(bool active)
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(active);
        }

        if (loadingBallRenderWorld != null)
        {
            loadingBallRenderWorld.SetActive(active);
        }
    }

    private void SetLoadingLogoActive(bool active)
    {
        if (loadingLogoArea != null)
        {
            loadingLogoArea.SetActive(active);
        }
    }

    private void SetLoadingText()
    {
        if (loadingText == null)
        {
            return;
        }

        loadingText.text = string.IsNullOrWhiteSpace(loadingTextValue)
            ? "Loading..."
            : loadingTextValue;
    }

    private void ForceOverlayCanvasToTop()
    {
        if (!forceOverlayToTop || overlayCanvas == null)
        {
            return;
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = overlaySortingOrder;
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (overlayCanvasGroup == null && overlayRoot != null)
        {
            overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();

            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayRoot.AddComponent<CanvasGroup>();
            }
        }

        if (overlayRoot == null && overlayCanvasGroup != null)
        {
            overlayRoot = overlayCanvasGroup.gameObject;
        }

        if (overlayCanvas == null && overlayRoot != null)
        {
            overlayCanvas = overlayRoot.GetComponentInParent<Canvas>(true);
        }
    }

    private void ApplyFadeStartAudio()
    {
        if (hasAppliedFadeStartAudio)
        {
            return;
        }

        hasAppliedFadeStartAudio = true;

        if (AudioManager.instance == null)
        {
            return;
        }

        AudioManager.instance.ApplyZoneMusicForCurrentScene();
    }

    #endregion
}
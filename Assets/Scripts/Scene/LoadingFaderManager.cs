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
    [SerializeField] private CanvasGroup loadingLogoCanvasGroup;
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
    [SerializeField] private float logoFadeOutTime = 0.25f;
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
        SetLoadingLogoAlpha(1f);
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

        if (overlayCanvasGroup == null)
        {
            HideInstant();
            yield break;
        }

        float timer = 0f;

        float overlayStartAlpha = overlayCanvasGroup.alpha;
        float overlayDuration = Mathf.Max(0.01f, fadeOutTime);

        float logoStartAlpha = loadingLogoCanvasGroup != null ? loadingLogoCanvasGroup.alpha : 1f;
        float logoDuration = Mathf.Max(0.01f, logoFadeOutTime);
        bool logoHidden = false;

        while (timer < overlayDuration)
        {
            timer += Time.unscaledDeltaTime;

            float overlayPercent = Mathf.Clamp01(timer / overlayDuration);
            overlayCanvasGroup.alpha = Mathf.Lerp(overlayStartAlpha, 0f, overlayPercent);

            if (loadingLogoCanvasGroup != null && !logoHidden)
            {
                float logoPercent = Mathf.Clamp01(timer / logoDuration);
                loadingLogoCanvasGroup.alpha = Mathf.Lerp(logoStartAlpha, 0f, logoPercent);

                if (logoPercent >= 1f)
                {
                    SetLoadingLogoActive(false);
                    logoHidden = true;
                }
            }

            yield return null;
        }

        HideInstant();
    }

    public void HideInstant()
    {
        ResolveReferences();

        isShowing = false;
        SetLoadingLogoAlpha(0f);
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

    private void SetLoadingLogoAlpha(float alpha)
    {
        if (loadingLogoCanvasGroup != null)
        {
            loadingLogoCanvasGroup.alpha = alpha;
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

        if (loadingLogoArea == null && loadingLogoCanvasGroup != null)
        {
            loadingLogoArea = loadingLogoCanvasGroup.gameObject;
        }

        if (loadingLogoCanvasGroup == null && loadingLogoArea != null)
        {
            loadingLogoCanvasGroup = loadingLogoArea.GetComponent<CanvasGroup>();

            if (loadingLogoCanvasGroup == null)
            {
                loadingLogoCanvasGroup = loadingLogoArea.AddComponent<CanvasGroup>();
            }
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
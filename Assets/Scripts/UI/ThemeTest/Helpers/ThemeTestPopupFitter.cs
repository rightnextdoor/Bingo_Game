using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ThemeTestPopupFitter : MonoBehaviour
{
    [Tooltip("The popup Scroll View's Viewport. Found automatically when left empty.")]
    [SerializeField] private RectTransform viewport;

    [Tooltip("Additional space above and below popups, beyond the layout group's padding.")]
    [Min(0f)]
    [SerializeField] private float extraVerticalPadding = 0f;

    private RectTransform content;
    private HorizontalLayoutGroup layoutGroup;
    private bool fitting;
    private readonly Vector3[] corners = new Vector3[4];

    private void OnEnable()
    {
        content = (RectTransform)transform;
        layoutGroup = GetComponent<HorizontalLayoutGroup>();

        _ = CanvasUpdateRegistry.instance;
        Canvas.preWillRenderCanvases += PreparePopupRoots;
        Canvas.willRenderCanvases += FitPopups;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= PreparePopupRoots;
        Canvas.willRenderCanvases -= FitPopups;
    }

    private void PreparePopupRoots()
    {
        if (!isActiveAndEnabled || content == null)
            return;

        for (int i = 0; i < content.childCount; i++)
        {
            if (!(content.GetChild(i) is RectTransform popup))
                continue;

            bool invalidWidth = popup.rect.width <= 0f;
            bool invalidHeight = popup.rect.height <= 0f;
            if (!invalidWidth && !invalidHeight)
                continue;

            Vector2 panelSize = Vector2.zero;
            for (int j = 0; j < popup.childCount; j++)
            {
                if (!(popup.GetChild(j) is RectTransform panel) || panel.anchorMin != panel.anchorMax)
                    continue;

                panelSize = Vector2.Max(panelSize, panel.rect.size);
            }

            if (invalidWidth && panelSize.x > 0f)
                popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelSize.x);
            if (invalidHeight && panelSize.y > 0f)
                popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelSize.y);
        }
    }

    private void EncapsulateVisibleRects(RectTransform root, RectTransform rect, ref Bounds bounds)
    {
        rect.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++)
            bounds.Encapsulate(root.InverseTransformPoint(corners[i]));

        if ((rect.TryGetComponent(out RectMask2D rectMask) && rectMask.isActiveAndEnabled)
            || (rect.TryGetComponent(out Mask mask) && mask.isActiveAndEnabled))
            return;

        for (int i = 0; i < rect.childCount; i++)
        {
            if (rect.GetChild(i) is RectTransform child && child.gameObject.activeInHierarchy)
                EncapsulateVisibleRects(root, child, ref bounds);
        }
    }

    private void FindViewport()
    {
        if (viewport != null)
            return;

        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.content == content)
            viewport = scroll.viewport;
    }

    private void FitPopups()
    {
        if (fitting || !isActiveAndEnabled || content == null)
            return;

        FindViewport();
        if (viewport == null)
            return;

        if (layoutGroup == null)
            layoutGroup = GetComponent<HorizontalLayoutGroup>();

        Vector3 viewportHeight = viewport.TransformVector(Vector3.up * viewport.rect.height);
        float availableHeight = content.InverseTransformVector(viewportHeight).magnitude;
        if (layoutGroup != null && layoutGroup.isActiveAndEnabled)
            availableHeight -= layoutGroup.padding.vertical;

        availableHeight -= Mathf.Max(0f, extraVerticalPadding) * 2f;
        if (availableHeight <= 0f || float.IsNaN(availableHeight) || float.IsInfinity(availableHeight))
            return;

        fitting = true;
        bool changed = false;
        try
        {
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform popup = content.GetChild(i) as RectTransform;
                if (popup == null || !popup.gameObject.activeInHierarchy)
                    continue;

                if (popup.TryGetComponent(out LayoutElement element) && element.ignoreLayout)
                    continue;

                Bounds bounds = new Bounds(popup.rect.center, popup.rect.size);
                EncapsulateVisibleRects(popup, popup, ref bounds);

                float height = 2f * Mathf.Max(
                    bounds.max.y - popup.rect.center.y,
                    popup.rect.center.y - bounds.min.y);
                if (height <= 0f || float.IsNaN(height) || float.IsInfinity(height))
                    continue;

                float scale = Mathf.Min(1f, availableHeight / height);
                Vector3 current = popup.localScale;
                if (Mathf.Approximately(current.x, scale) && Mathf.Approximately(current.y, scale))
                    continue;

                popup.localScale = new Vector3(scale, scale, current.z);
                changed = true;
            }
        }
        finally
        {
            fitting = false;
        }

        if (changed)
            LayoutRebuilder.MarkLayoutForRebuild(content);
    }
}

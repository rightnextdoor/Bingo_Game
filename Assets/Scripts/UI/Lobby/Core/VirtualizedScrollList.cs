using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class VirtualizedScrollList : MonoBehaviour
{
    #region Fields

    private readonly List<PooledItem> pooledItems = new List<PooledItem>();

    private ScrollRect scrollRect;
    private RectTransform content;
    private RectTransform viewport;
    private GameObject itemPrefab;

    private int itemCount;
    private int extraVisibleItems = 2;

    private float itemHeight = 1f;
    private float spacing;
    private float paddingTop;
    private float paddingBottom;

    private int lastStartIndex = -1;
    private int lastEndIndex = -1;
    private bool initialized;

    public event Action<GameObject, int> ItemBound;
    public event Action<GameObject, int> ItemReleased;

    public int ItemCount => itemCount;
    public int ActiveItemCount { get; private set; }
    public bool IsInitialized => initialized;

    #endregion

    #region Setup

    public bool Initialize(
        ScrollRect targetScrollRect,
        RectTransform targetContent,
        GameObject targetItemPrefab,
        VerticalLayoutGroup existingLayoutGroup = null,
        ContentSizeFitter existingContentSizeFitter = null,
        float itemHeightOverride = 0f,
        int extraVisibleItemCount = 2)
    {
        if (targetScrollRect == null || targetContent == null || targetItemPrefab == null)
        {
            return false;
        }

        UnsubscribeFromScroll();

        scrollRect = targetScrollRect;
        content = targetContent;
        viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        itemPrefab = targetItemPrefab;
        extraVisibleItems = Mathf.Max(0, extraVisibleItemCount);

        CaptureLayout(existingLayoutGroup, existingContentSizeFitter);
        itemHeight = ResolveItemHeight(itemHeightOverride);
        ConfigureContentTransform();

        ClearPool();
        ClearExistingContentChildren();

        itemPrefab.SetActive(false);

        itemCount = 0;
        lastStartIndex = -1;
        lastEndIndex = -1;
        initialized = viewport != null && itemHeight > 0f;

        if (!initialized)
        {
            return false;
        }

        SubscribeToScroll();
        UpdateContentHeight();

        return true;
    }

    private void CaptureLayout(VerticalLayoutGroup existingLayoutGroup, ContentSizeFitter existingContentSizeFitter)
    {
        if (existingLayoutGroup != null)
        {
            spacing = existingLayoutGroup.spacing;
            paddingTop = existingLayoutGroup.padding.top;
            paddingBottom = existingLayoutGroup.padding.bottom;
            existingLayoutGroup.enabled = false;
        }
        else
        {
            spacing = 0f;
            paddingTop = 0f;
            paddingBottom = 0f;
        }

        if (existingContentSizeFitter != null)
        {
            existingContentSizeFitter.enabled = false;
        }
    }

    private float ResolveItemHeight(float itemHeightOverride)
    {
        if (itemHeightOverride > 0f)
        {
            return itemHeightOverride;
        }

        RectTransform prefabRect = itemPrefab.GetComponent<RectTransform>();
        LayoutElement layoutElement = itemPrefab.GetComponent<LayoutElement>();

        if (layoutElement != null && layoutElement.preferredHeight > 0f)
        {
            return layoutElement.preferredHeight;
        }

        if (prefabRect != null && prefabRect.rect.height > 0f)
        {
            return prefabRect.rect.height;
        }

        if (prefabRect != null)
        {
            float preferredHeight = LayoutUtility.GetPreferredHeight(prefabRect);

            if (preferredHeight > 0f)
            {
                return preferredHeight;
            }
        }

        return 1f;
    }

    private void ConfigureContentTransform()
    {
        Vector2 anchorMin = content.anchorMin;
        Vector2 anchorMax = content.anchorMax;
        Vector2 pivot = content.pivot;

        anchorMin.y = 1f;
        anchorMax.y = 1f;
        pivot.y = 1f;

        content.anchorMin = anchorMin;
        content.anchorMax = anchorMax;
        content.pivot = pivot;
    }

    private void ClearExistingContentChildren()
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (child == null)
            {
                continue;
            }

            GameObject childObject = child.gameObject;

            // If the configured item prefab is actually a scene template under
            // Content, keep the template but make sure it cannot render.
            if (childObject == itemPrefab)
            {
                childObject.SetActive(false);
                continue;
            }

            childObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(childObject);
            }
            else
            {
                DestroyImmediate(childObject);
            }
        }
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        UnsubscribeFromScroll();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            SubscribeToScroll();
            RefreshVisibleItems(true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromScroll();
        ClearPool();
    }

    #endregion

    #region Data

    public void SetItemCount(int count)
    {
        if (!initialized)
        {
            return;
        }

        itemCount = Mathf.Max(0, count);

        UpdateContentHeight();
        ClampContentPosition();
        EnsurePoolCapacity();
        RefreshVisibleItems(true);
    }

    public void RefreshVisibleItems(bool forceRebind = false)
    {
        if (!initialized)
        {
            return;
        }

        if (itemCount <= 0)
        {
            ReleaseAllItems();

            lastStartIndex = -1;
            lastEndIndex = -1;

            return;
        }

        GetVisibleRange(out int startIndex, out int endIndex);

        if (!forceRebind && startIndex == lastStartIndex && endIndex == lastEndIndex)
        {
            return;
        }

        lastStartIndex = startIndex;
        lastEndIndex = endIndex;

        int activeCount = endIndex >= startIndex ? endIndex - startIndex + 1 : 0;

        EnsurePoolCapacity(activeCount);

        for (int i = 0; i < pooledItems.Count; i++)
        {
            int targetIndex = i < activeCount ? startIndex + i : -1;
            BindPoolItem(pooledItems[i], targetIndex, forceRebind);
        }

        ActiveItemCount = activeCount;
    }

    public bool RefreshItem(int index)
    {
        if (!initialized || index < 0 || index >= itemCount)
        {
            return false;
        }

        for (int i = 0; i < pooledItems.Count; i++)
        {
            PooledItem pooledItem = pooledItems[i];

            if (pooledItem.boundIndex != index ||
                pooledItem.gameObject == null ||
                !pooledItem.gameObject.activeSelf)
            {
                continue;
            }

            ItemBound?.Invoke(pooledItem.gameObject, index);

            return true;
        }

        return false;
    }

    public bool IsIndexVisible(int index)
    {
        if (!initialized || index < 0 || index >= itemCount)
        {
            return false;
        }

        for (int i = 0; i < pooledItems.Count; i++)
        {
            PooledItem pooledItem = pooledItems[i];

            if (pooledItem.boundIndex == index &&
                pooledItem.gameObject != null &&
                pooledItem.gameObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetVisibleItem(int index, out GameObject itemObject)
    {
        itemObject = null;

        for (int i = 0; i < pooledItems.Count; i++)
        {
            PooledItem pooledItem = pooledItems[i];

            if (pooledItem.boundIndex != index ||
                pooledItem.gameObject == null ||
                !pooledItem.gameObject.activeSelf)
            {
                continue;
            }

            itemObject = pooledItem.gameObject;

            return true;
        }

        return false;
    }

    #endregion

    #region Scrolling

    public void ScrollToIndex(int index)
    {
        if (!initialized || itemCount <= 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, itemCount - 1);

        float viewportHeight = Mathf.Max(0f, viewport.rect.height);
        float contentHeight = content.rect.height;
        float step = itemHeight + spacing;

        float itemTop = paddingTop + (index * step);
        float itemBottom = itemTop + itemHeight;

        float maxTop = Mathf.Max(0f, contentHeight - viewportHeight);
        float currentTop = Mathf.Clamp(content.anchoredPosition.y, 0f, maxTop);
        float currentBottom = currentTop + viewportHeight;
        float targetTop = currentTop;

        if (itemTop < currentTop)
        {
            targetTop = itemTop;
        }
        else if (itemBottom > currentBottom)
        {
            targetTop = itemBottom - viewportHeight;
        }

        targetTop = Mathf.Clamp(targetTop, 0f, maxTop);

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetTop);

        scrollRect.StopMovement();
        RefreshVisibleItems(true);
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        RefreshVisibleItems();
    }

    private void GetVisibleRange(out int startIndex, out int endIndex)
    {
        float step = Mathf.Max(1f, itemHeight + spacing);
        float viewportHeight = Mathf.Max(1f, viewport.rect.height);
        float scrollOffset = Mathf.Max(0f, content.anchoredPosition.y - paddingTop);

        int firstVisibleIndex = Mathf.FloorToInt(scrollOffset / step);
        int visibleItemCount = Mathf.CeilToInt(viewportHeight / step) + 1;

        startIndex = Mathf.Clamp(firstVisibleIndex - extraVisibleItems, 0, Mathf.Max(0, itemCount - 1));
        endIndex = Mathf.Clamp(firstVisibleIndex + visibleItemCount + extraVisibleItems - 1, 0, itemCount - 1);
    }

    private void ClampContentPosition()
    {
        if (content == null || viewport == null)
        {
            return;
        }

        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        float clampedY = Mathf.Clamp(content.anchoredPosition.y, 0f, maxY);

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, clampedY);
    }

    #endregion

    #region Pool

    private void EnsurePoolCapacity()
    {
        if (!initialized)
        {
            return;
        }

        float step = Mathf.Max(1f, itemHeight + spacing);
        int visibleCount = Mathf.CeilToInt(Mathf.Max(1f, viewport.rect.height) / step) + 1;

        EnsurePoolCapacity(Mathf.Min(itemCount, visibleCount + (extraVisibleItems * 2)));
    }

    private void EnsurePoolCapacity(int requiredCount)
    {
        requiredCount = Mathf.Max(0, requiredCount);

        while (pooledItems.Count < requiredCount)
        {
            GameObject itemObject = Instantiate(itemPrefab, content);

            itemObject.name = $"VirtualItem_{pooledItems.Count}";
            itemObject.SetActive(false);

            RectTransform itemRect = itemObject.GetComponent<RectTransform>();

            if (itemRect != null)
            {
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(1f, 1f);
                itemRect.pivot = new Vector2(0.5f, 1f);
                itemRect.sizeDelta = new Vector2(0f, itemHeight);
            }

            pooledItems.Add(new PooledItem(itemObject, itemRect));
        }
    }

    private void BindPoolItem(PooledItem pooledItem, int targetIndex, bool forceRebind)
    {
        if (pooledItem == null || pooledItem.gameObject == null)
        {
            return;
        }

        if (targetIndex < 0 || targetIndex >= itemCount)
        {
            ReleasePoolItem(pooledItem);
            return;
        }

        bool indexChanged = pooledItem.boundIndex != targetIndex;

        if (indexChanged && pooledItem.boundIndex >= 0)
        {
            ItemReleased?.Invoke(pooledItem.gameObject, pooledItem.boundIndex);
        }

        pooledItem.boundIndex = targetIndex;

        PositionItem(pooledItem, targetIndex);

        if (!pooledItem.gameObject.activeSelf)
        {
            pooledItem.gameObject.SetActive(true);
        }

        if (indexChanged || forceRebind)
        {
            ItemBound?.Invoke(pooledItem.gameObject, targetIndex);
        }
    }

    private void ReleasePoolItem(PooledItem pooledItem)
    {
        if (pooledItem == null || pooledItem.gameObject == null)
        {
            return;
        }

        if (pooledItem.boundIndex >= 0)
        {
            ItemReleased?.Invoke(pooledItem.gameObject, pooledItem.boundIndex);
        }

        pooledItem.boundIndex = -1;

        if (pooledItem.gameObject.activeSelf)
        {
            pooledItem.gameObject.SetActive(false);
        }
    }

    private void ReleaseAllItems()
    {
        for (int i = 0; i < pooledItems.Count; i++)
        {
            ReleasePoolItem(pooledItems[i]);
        }

        ActiveItemCount = 0;
    }

    private void PositionItem(PooledItem pooledItem, int index)
    {
        if (pooledItem.rectTransform == null)
        {
            return;
        }

        float y = paddingTop + (index * (itemHeight + spacing));

        pooledItem.rectTransform.anchoredPosition = new Vector2(0f, -y);
    }

    private void UpdateContentHeight()
    {
        if (content == null)
        {
            return;
        }

        float totalHeight = paddingTop + paddingBottom;

        if (itemCount > 0)
        {
            totalHeight += itemCount * itemHeight;
            totalHeight += Mathf.Max(0, itemCount - 1) * spacing;
        }

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, totalHeight));
    }

    private void ClearPool()
    {
        for (int i = pooledItems.Count - 1; i >= 0; i--)
        {
            GameObject itemObject = pooledItems[i]?.gameObject;

            if (itemObject == null)
            {
                continue;
            }

            itemObject.SetActive(false);

            // Detach immediately so a Destroy scheduled for the end of the frame
            // cannot remain inside Content and briefly render as a stale list item.
            if (content != null && itemObject.transform.parent == content)
            {
                itemObject.transform.SetParent(null, false);
            }

            if (Application.isPlaying)
            {
                Destroy(itemObject);
            }
            else
            {
                DestroyImmediate(itemObject);
            }
        }

        pooledItems.Clear();
        ActiveItemCount = 0;
    }

    #endregion

    #region Events

    private void SubscribeToScroll()
    {
        if (scrollRect == null)
        {
            return;
        }

        scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    private void UnsubscribeFromScroll()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }
    }

    #endregion

    private sealed class PooledItem
    {
        public readonly GameObject gameObject;
        public readonly RectTransform rectTransform;

        public int boundIndex = -1;

        public PooledItem(GameObject gameObject, RectTransform rectTransform)
        {
            this.gameObject = gameObject;
            this.rectTransform = rectTransform;
        }
    }
}
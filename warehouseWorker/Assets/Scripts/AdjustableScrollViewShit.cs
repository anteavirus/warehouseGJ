using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdjustableScrollViewShit : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private float maxHeight = 500f;

    private RectTransform scrollViewRect;
    private ScrollRect scrollRect;

    private void Awake()
    {
        if (content == null) { content = GetComponent<RectTransform>(); }
        scrollViewRect = GetComponent<RectTransform>();
        scrollRect = GetComponent<ScrollRect>();
        Resize();
    }

    private void Resize()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float contentHeight = content.rect.height;

        float newHeight = Mathf.Min(contentHeight, maxHeight);
        scrollViewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);

        bool enableScrolling = contentHeight > maxHeight;
        scrollRect.vertical = enableScrolling;

        if (scrollRect.verticalScrollbar != null)
            scrollRect.verticalScrollbar.gameObject.SetActive(enableScrolling);
    }
}

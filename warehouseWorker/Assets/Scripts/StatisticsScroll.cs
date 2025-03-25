using UnityEngine;
using UnityEngine.UI;

public class InfiniteScrollCarouselHand : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform[] handTransforms;
    [SerializeField] private RectTransform content;

    [Header("Settings")]
    [SerializeField] private float interval = 10f;
    [SerializeField] private float baseOffset = 5f;
    [SerializeField] private float viewportBuffer = 50f;

    private float viewportHeight;
    private float handVirtualY;
    private float scrollVelocity;

    private void Awake()
    {
        scrollRect.onValueChanged.AddListener(OnScroll);
        InitializeHand();
    }

    private void InitializeHand()
    {
        viewportHeight = scrollRect.viewport.rect.height;
        handVirtualY = baseOffset;
        UpdateHandPosition();
    }

    private void OnScroll(Vector2 position)
    {
        // Update visibility and position
        CheckHandVisibility();
        UpdateHandPosition();
    }

    private void CheckHandVisibility()
    {
        float handViewportY = handVirtualY - content.anchoredPosition.y;
        bool wasOutsideBounds = false;

        // Check bottom boundary
        if (handViewportY < -viewportHeight / 2 - viewportBuffer)
        {
            // Scroll direction is down
            float jumpSteps = Mathf.Ceil((viewportHeight + viewportBuffer * 2) / interval);
            handVirtualY += jumpSteps * interval;
            wasOutsideBounds = true;
        }
        // Check top boundary
        else if (handViewportY > viewportHeight / 2 + viewportBuffer)
        {
            // Scroll direction is up
            float jumpSteps = Mathf.Ceil((viewportHeight + viewportBuffer * 2) / interval);
            handVirtualY -= jumpSteps * interval;
            wasOutsideBounds = true;
        }

        if (wasOutsideBounds)
        {
            // Snap content to bring hand into view
            SnapToNearestInterval();
        }
    }

    private void UpdateHandPosition()
    {
        foreach (var hand in handTransforms)
        {

            Vector2 newPos = new Vector2(
                hand.anchoredPosition.x,
                handVirtualY - content.anchoredPosition.y
            );

            hand.anchoredPosition = newPos;
        }
    }

    private void SnapToNearestInterval()
    {
        float currentPos = handVirtualY;
        float nearestInterval = Mathf.Round((currentPos - baseOffset) / interval) * interval + baseOffset;
        float targetY = nearestInterval;

        foreach (var hand in handTransforms)
        {
            content.anchoredPosition = new Vector2(
                content.anchoredPosition.x,
                Mathf.SmoothDamp(content.anchoredPosition.y, targetY - hand.anchoredPosition.y, ref scrollVelocity, 0.2f)
            );
        }
    }

    private void LateUpdate()
    {
        if (Input.GetMouseButtonUp(0))
        {
            SnapToNearestInterval();
        }
    }
}

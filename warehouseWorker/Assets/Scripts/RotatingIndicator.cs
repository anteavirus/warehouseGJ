using System.Collections;
using UnityEngine;

public class RotatingIndicator : MonoBehaviour
{
    public Transform targetTransform { get; private set; }
    private GameObject sprite1;
    private GameObject sprite2;
    private float rotationSpeed;
    private float bounceHeight = 0.5f;
    private float bounceDuration = 1.0f;
    private Coroutine rotationCoroutine;
    private bool isActive = false;
    private Vector3 basePosition;
    private float minHeightOffset = 1.2f; // Minimum height above target
    private float animationStartTime;

    public void Initialize(Transform target, Sprite spriteAsset, float speed, float yScale = 1f, float bounceHeight = .5f, float bounceDuration = 4.0f)
    {
        gameObject.layer = LayerMask.NameToLayer("Interactable");
        targetTransform = target;
        rotationSpeed = speed;
        this.bounceHeight = bounceHeight;
        this.bounceDuration = bounceDuration;

        // Create sprites first, then calculate scale
        sprite1 = CreateSpriteObject("IndicatorSprite1", spriteAsset, yScale, 1f); // Temp scale
        sprite2 = CreateSpriteObject("IndicatorSprite2", spriteAsset, yScale, 1f); // Temp scale

        sprite1.transform.SetParent(transform);
        sprite2.transform.SetParent(transform);

        // Position sprites to form a cross for Y-axis rotation
        sprite1.transform.localPosition = Vector3.zero;
        sprite2.transform.localPosition = Vector3.zero;
        sprite2.transform.Rotate(0, 90, 0); // 90 degrees around Y-axis for cross effect

        // Set animation start time with random offset for variety, but ensure it's valid
        float randomOffset = Random.Range(0f, bounceDuration);
        animationStartTime = Time.time - randomOffset;

        UpdateBasePosition();
        isActive = true;
        StartRotation();
    }

    private GameObject CreateSpriteObject(string name, Sprite spriteAsset, float iHateMyJob, float baseScale)
    {
        GameObject spriteObj = new GameObject(name);
        SpriteRenderer spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = spriteAsset;

        spriteRenderer.sortingOrder = 100;

        Vector3 scale = Vector3.one * baseScale;
        scale *= iHateMyJob;
        spriteObj.transform.localScale = scale;

        return spriteObj;
    }

    public bool IsValid()
    {
        return isActive && sprite1 != null && sprite2 != null && targetTransform != null;
    }

    private void StartRotation()
    {
        if (rotationCoroutine != null)
            StopCoroutine(rotationCoroutine);

        rotationCoroutine = StartCoroutine(RotateAndBounceCoroutine());
    }

    private void StopRotation()
    {
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }
    }

    private IEnumerator RotateAndBounceCoroutine()
    {
        while (isActive && targetTransform != null)
        {
            UpdateBasePosition();

            float elapsedTime = Time.time - animationStartTime;

            elapsedTime = Mathf.Max(0f, elapsedTime);

            float rotationCycles = elapsedTime * rotationSpeed / 360f;
            float rotationAngle = (rotationCycles % 1f) * 360f;
            transform.rotation = Quaternion.Euler(0, rotationAngle, 0);

            float bounceTime = elapsedTime % bounceDuration;
            float bounceProgress = bounceTime / bounceDuration;

            float easedProgress = EaseInOutQuad(bounceProgress);
            float bounceOffset = Mathf.Sin(easedProgress * Mathf.PI) * bounceHeight;

            Vector3 newPosition = basePosition;
            newPosition.y += minHeightOffset + bounceOffset;
            transform.position = newPosition;

            yield return null;
        }
    }

    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    private void UpdateBasePosition()
    {
        if (targetTransform != null)
        {
            // Get the target's bounds to position above it properly
            Renderer targetRenderer = targetTransform.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                // Position above the top of the object's bounds
                basePosition = targetRenderer.bounds.center;
                minHeightOffset = targetRenderer.bounds.size.y * 0.5f + 0.3f;
            }
            else
            {
                // Fallback if no renderer found
                basePosition = targetTransform.position;
                minHeightOffset = 1.2f;
            }
        }
    }

    public void SetBounceParameters(float height, float duration)
    {
        bounceHeight = height;
        bounceDuration = duration;
    }

    // Method to reset animation timing (useful for restarting or synchronizing)
    public void ResetAnimationTiming(float? customOffset = null)
    {
        float offset = customOffset ?? Random.Range(0f, bounceDuration);
        animationStartTime = Time.time - offset;
    }

    // Method to get current animation progress for debugging
    public float GetAnimationProgress()
    {
        if (!isActive) return 0f;

        float elapsedTime = Time.time - animationStartTime;
        return (elapsedTime % bounceDuration) / bounceDuration;
    }

    // Method to synchronize with another indicator
    public void SynchronizeWith(RotatingIndicator other)
    {
        if (other != null && other.isActive)
        {
            this.animationStartTime = other.animationStartTime;
        }
    }

    public void Disable()
    {
        isActive = false;
        StopRotation();

        if (sprite1 != null) Destroy(sprite1);
        if (sprite2 != null) Destroy(sprite2);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        StopRotation();
    }
}

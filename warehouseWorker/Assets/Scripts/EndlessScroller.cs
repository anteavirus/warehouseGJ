using UnityEngine;
using UnityEngine.UI;

//[RequireComponent(typeof(Renderer), typeof(RawImage))]
public class EndlessScroller : MonoBehaviour
{
    [SerializeField] Vector2 scrollSpeed = new Vector2(0.1f, 0.1f);

    Material material;
    RawImage rawImage;
    SpriteRenderer spriteRenderer;
    Vector2 uvOffset = Vector2.zero;
    readonly float screenAspectMultiplier = 5;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        UpdateScreenRect();
        SetRandomColor();
    }

    void UpdateScreenRect()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float screenAspect = screenWidth / screenHeight;

        rawImage.uvRect = new Rect(0, 0, screenAspect * screenAspectMultiplier, 1 * screenAspectMultiplier);
    }

    public void SetRandomColor()
    {
        // Try to get components with priority on UI elements first
        if (TryGetComponent(out rawImage))
        {
            // For UI RawImage
            rawImage.color = GiveMeNotDarkColor();
        }
        else if (TryGetComponent(out spriteRenderer))
        {
            // For SpriteRenderer (2D objects)
            material = new Material(spriteRenderer.material);
            spriteRenderer.material = material;
            material.color = GiveMeNotDarkColor();
        }
    }

    Color GiveMeNotDarkColor()
    {
        float h = Random.Range(0f, 1f);
        float s = Random.Range(0.5f, 1f);
        float v = Random.Range(0.8f, 1f);
        return Color.HSVToRGB(h, s, v);
    }

    void Update()
    {
        UpdateScreenRect();

        uvOffset.x = Mathf.Repeat(uvOffset.x + scrollSpeed.x * Time.unscaledDeltaTime, 1f);
        uvOffset.y = Mathf.Repeat(uvOffset.y + scrollSpeed.y * Time.unscaledDeltaTime, 1f);

        if (rawImage != null)
        {
            rawImage.uvRect = new Rect(uvOffset, rawImage.uvRect.size);
        }
        else if (material != null)
        {
            material.mainTextureOffset = uvOffset;
        }
    }
}

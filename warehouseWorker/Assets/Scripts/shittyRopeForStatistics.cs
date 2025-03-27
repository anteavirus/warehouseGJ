using UnityEngine;
using UnityEngine.UI;

public class ShittyRopeForStatistics : MonoBehaviour
{
    [Header("References")]
    public Scrollbar scrollbar;
    public RawImage ropeRawImage;

    [Header("Settings")]
    public float textureScrollSpeed = 1f;
    public bool verticalScroll = true;
    public Vector2 tiling = Vector2.one;

    private Material ropeMaterial;
    private Vector2 initialOffset;
    private float previousValue;

    void Start()
    {
        if (ropeRawImage != null)
        {
            // Clone material to prevent changing all instances
            ropeMaterial = Instantiate(ropeRawImage.material);
            ropeRawImage.material = ropeMaterial;

            initialOffset = ropeMaterial.mainTextureOffset;
            ropeMaterial.mainTextureScale = tiling;
        }

        scrollbar.onValueChanged.AddListener(HandleScroll);
        previousValue = scrollbar.value;
    }

    void HandleScroll(float value)
    {
        if (ropeMaterial == null) return;

        float delta = previousValue - value;
        previousValue = value;

        Vector2 offset = ropeMaterial.mainTextureOffset;

        if (verticalScroll)
        {
            offset.y += delta * textureScrollSpeed;
            offset.y = Mathf.Repeat(offset.y, 1f);
        }
        else
        {
            offset.x += delta * textureScrollSpeed;
            offset.x = Mathf.Repeat(offset.x, 1f);
        }

        ropeMaterial.mainTextureOffset = offset;
    }

    void OnDestroy()
    {
        // Reset material if needed
        if (ropeRawImage != null && ropeMaterial != null)
        {
            Destroy(ropeMaterial);
        }
    }
}

using UnityEngine;

public class PixelateController : MonoBehaviour
{
    [SerializeField] private PixelateRendererFetaure pixelateFeature;
    [SerializeField] private float pixelSize = 64;

    void Update()
    {
        if (pixelateFeature != null)
            pixelateFeature.settings.pixelSize = pixelSize;
    }
}

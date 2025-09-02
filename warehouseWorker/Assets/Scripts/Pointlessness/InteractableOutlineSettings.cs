using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using System.Collections;

public class RenderFeatureOutlineFader : MonoBehaviour
{
    // TODO: some day, some how, patch in a solution where i can actually CREATE A DUPLICATE OF A MATERIAL that CAN BE USED instead of just lingering unused in the memory.
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");

    [Header("References")]
    [SerializeField] private RenderObjects outlineFeature;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1f;

    private Coroutine currentFadeRoutine;
    private Color originalOutlineColor = Color.white; 
    [SerializeField] private Color defaultOutlineColor = Color.white;   // ????: Safeguard() in OnValidate() needs??? THIS to NOT turn into a `new(0,0,0,0)`
                                                                        // But not always??? for some reason it didn't turn into black w/o this all. :/

    void Awake()
    {
        if (outlineFeature == null)
        {
            Debug.LogError("OutlineFeature reference missing!", this);
            return;
        }

        if (outlineFeature.settings.overrideMaterial != null)
        {
            originalOutlineColor = defaultOutlineColor;
            outlineFeature.settings.overrideMaterial.SetColor(OutlineColorID, defaultOutlineColor);
        }
    }

    public void FadeOutline(float targetAlpha)
    {
        if (currentFadeRoutine != null)
            StopCoroutine(currentFadeRoutine);

        currentFadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        Material activeMaterial = outlineFeature.settings.overrideMaterial;
        if (activeMaterial == null) yield break;

        Color startColor = activeMaterial.GetColor(OutlineColorID);
        float startAlpha = startColor.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            // Directly modify the material's alpha
            activeMaterial.SetColor(OutlineColorID, new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                Mathf.Lerp(startAlpha, targetAlpha, t)
            ));

            yield return null;
        }

        // Ensure final alpha
        activeMaterial.SetColor(OutlineColorID, new Color(
            startColor.r,
            startColor.g,
            startColor.b,
            targetAlpha
        ));
    }

    void OnApplicationQuit()
    {
        Safeguard();
    }

    void OnValidate()
    {
        Safeguard();
    }

    void OnDisable()
    {
        Safeguard();
    }

    void OnDestroy()
    {
        Safeguard();
    }

    void Safeguard()
    {
        // Revert to original alpha ONLY if this was the last active fader
        if (originalOutlineColor.a != outlineFeature.settings.overrideMaterial.GetColor(OutlineColorID).a)
        {
            outlineFeature.settings.overrideMaterial.SetColor(OutlineColorID, defaultOutlineColor);
        }
    }
}
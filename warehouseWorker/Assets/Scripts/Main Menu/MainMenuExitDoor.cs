using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource), typeof(Animator))]
public class MainMenuExitDoor : MonoBehaviour
{
    [SerializeField]
    AudioClip[] exitClips;
    [SerializeField] private GameObject blackPanel;
    private bool isExiting = false;
    AudioSource source;
    Animator animator;

    private void Start()
    {
        if (blackPanel != null)
            blackPanel.SetActive(false);

        source = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    private void OnMouseOver()
    {
        if (IsPointerOverUI()) return;
        animator.SetBool("open", true);
    }

    private void OnMouseEnter()
    {
        if (IsPointerOverUI()) return;
        animator.SetBool("open", true);
    }

    private void OnMouseExit()
    {
        if (IsPointerOverUI()) return;
        animator.SetBool("open", false);
    }

    private void OnMouseDown()
    {
        if (IsPointerOverUI()) return;
        if (!isExiting)
        {
            StartExitSequence();
        }
    }

    private void StartExitSequence()
    {
        isExiting = true;

        // I now realize i could make this it's own class instead of writing this a thousand times
        if (exitClips != null && exitClips.Length > 0)
        {
            source.PlayOneShot(exitClips[Random.Range(0, exitClips.Length)]);
        }

        if (blackPanel != null)
        {
            blackPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Black panel reference not set in MainMenuExitDoor!");
        }

        StartCoroutine(QuitApplication());
    }

    private IEnumerator QuitApplication()
    {
        yield return new WaitForSecondsRealtime(5f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        // Check against all UI elements using GraphicRaycaster
        PointerEventData eventData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        // Return true ONLY if a UI element is hit (filtered by GraphicRaycaster)
        foreach (RaycastResult result in results)
        {
            if (result.module is GraphicRaycaster)
                return true;
        }
        return false;
    }

    Coroutine goingTransparentOnce;

    public void GoTransparent()
    {
        if (goingTransparentOnce != null)
        {
            return;
        }
        goingTransparentOnce = StartCoroutine(GoingTransparent());
    }

    IEnumerator GoingTransparent()
    {
        List<Renderer> allRenderers = new List<Renderer>();
        List<TMPro.TextMeshProUGUI> allTexts = new List<TMPro.TextMeshProUGUI>();

        GetAllRenderers(transform, allRenderers);
        GetAllTexts(transform, allTexts);

        allRenderers.ForEach(rend => {
            if (rend != null && rend.material != null)
                SetupMaterialForTransparency(rend.material);
        });

        allRenderers.ForEach(item => {
            if (item != null && item.gameObject != null)
                item.gameObject.layer = 0;
        });

        float timer = 0f, fadeDuration = 1f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / fadeDuration);

            allRenderers.ForEach(rend => {
                if (rend != null && rend.material != null)
                    rend.material.color = SetAlpha(rend.material.color, alpha);
            });

            allTexts.ForEach(text => {
                if (text != null)
                {
                    text.color = SetAlpha(text.color, alpha);
                }
            });
            yield return null;
        }

        allRenderers.ForEach(rend => {
            if (rend != null && rend.material != null)
                rend.material.color = SetAlpha(rend.material.color, 0f);
        });

        allTexts.ForEach(text => {
            if (text != null)
                text.color = SetAlpha(text.color, 0f);
        });
    }

    private void GetAllRenderers(Transform parent, List<Renderer> renderers)
    {
        Renderer renderer = parent.GetComponent<Renderer>();
        if (renderer != null && !renderers.Contains(renderer))
        {
            renderers.Add(renderer);
        }

        foreach (Transform child in parent)
        {
            GetAllRenderers(child, renderers);
        }
    }

    // TODO: unfuck this stupid shit. why the fuck is exit sign a UGUI text? what the fuck was i doing.
    private void GetAllTexts(Transform parent, List<TMPro.TextMeshProUGUI> texts)
    {
        TMPro.TextMeshProUGUI text = parent.GetComponent<TMPro.TextMeshProUGUI>();
        if (text != null && !texts.Contains(text))
        {
            texts.Add(text);
        }

        foreach (Transform child in parent)
        {
            GetAllTexts(child, texts);
        }
    }

    private Color SetAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    private void SetupMaterialForTransparency(Material material)
    {
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 2);
        }

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.EnableKeyword("_ALPHABLEND_ON");

        material.renderQueue = 3000;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0);
        }

        material.SetOverrideTag("RenderType", "Transparent");
    }
}

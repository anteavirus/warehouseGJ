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
}

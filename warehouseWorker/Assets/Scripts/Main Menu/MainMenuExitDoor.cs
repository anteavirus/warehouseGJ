using UnityEngine;
using System.Collections;

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
        animator.SetBool("open", true);
    }

    private void OnMouseExit()
    {
        animator.SetBool("open", false);
    }

    private void OnMouseDown()
    {
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
}

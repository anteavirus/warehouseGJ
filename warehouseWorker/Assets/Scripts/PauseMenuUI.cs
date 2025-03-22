using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator pauseAnimator;
    [SerializeField] private GameObject pauseVisuals; // Canvas/UI elements
    [SerializeField] private EndlessScroller endlessScroller;

    public bool isPaused;
    private Coroutine pauseRoutine;

    private void Start()
    {
        pauseAnimator.Play("invisiblePause", 0, 125f);
        pauseAnimator.Update(0f);
    }

    public void TogglePause()
    {
        if (pauseRoutine != null) return;

        isPaused = !isPaused;
        pauseVisuals.SetActive(true);
        pauseAnimator.updateMode = isPaused ?
            AnimatorUpdateMode.UnscaledTime :
            AnimatorUpdateMode.Normal;

        pauseAnimator.SetBool("Visible", isPaused);
        pauseRoutine = StartCoroutine(PauseStateUpdate());
    }

    IEnumerator PauseStateUpdate()
    {
        Time.timeScale = isPaused ? 0f : 1f;
        pauseAnimator.SetBool("animationComplete", false);

        yield return new WaitUntil(() =>
            pauseAnimator.GetBool("animationComplete"));

        if (!isPaused) pauseVisuals.SetActive(false);
        pauseAnimator.SetBool("animationComplete", false);
		
		Cursor.lockState = isPaused ?
			CursorLockMode.None :
			CursorLockMode.Locked;

		Cursor.visible = isPaused;
		
        pauseRoutine = null;
    }

    public void MarkAnimationComplete() =>
        pauseAnimator.SetBool("animationComplete", true);

    public void LoadScene(int id)
    {
        SceneManager.LoadScene(id);
    }

    public void LoadSceneOffset(int offset)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + offset);
    }

    public void ChangeColorOfScroll()
    {
        endlessScroller.SetRandomColor();
    }
}

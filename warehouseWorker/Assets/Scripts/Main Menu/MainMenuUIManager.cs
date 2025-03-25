using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI Animation")]
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private GameObject menuVisuals;
    [SerializeField] private string visibleState = "Visible";

    [Header("UI Controls")]
    [SerializeField] private Button showUIButton;
    [SerializeField] private Button hideUIButton;

    [Header("Game Settings")]
    public TMP_InputField usernameInput;
    public TMP_Dropdown difficultyDropdown;
    public MainMenuPlayerController playerController;

    private Coroutine animationRoutine;
    private bool isMenuVisible;

    private void Start()
    {
        InitializeUI();
        if (usernameInput) LoadGameSettings();
        else Debug.LogWarning("No username input. ALLOW THE PLAYERS TO PLAY THE GAME YOU DUMB");

        showUIButton.onClick.AddListener(() => SetMenuState(true));
        hideUIButton.onClick.AddListener(() => SetMenuState(false));
    }

    void InitializeUI()
    {
        menuVisuals.SetActive(true);
        menuAnimator.Play("ShowEverything", 0, 125f);
        menuAnimator.Update(0f);
        isMenuVisible = true;
    }

    public void SetMenuState(bool visible)
    {
        if (animationRoutine != null) return;

        isMenuVisible = visible;
        menuAnimator.SetBool(visibleState, visible);
        animationRoutine = StartCoroutine(AnimateMenuTransition());
    }

    IEnumerator AnimateMenuTransition()
    {
        menuAnimator.SetBool("animationComplete", false);
        if (isMenuVisible) menuVisuals.SetActive(true);

        yield return new WaitUntil(() =>
            menuAnimator.GetBool("animationComplete"));

        if (!isMenuVisible) menuVisuals.SetActive(false);
        menuAnimator.SetBool("animationComplete", false);

        playerController.STOPWORKINGIMINUI = isMenuVisible;

        animationRoutine = null;
    }

    /// <summary> I don't actually use this. It doesn't quite work? Use SelfAnimator thing instead. </summary>
    public void MarkAnimationComplete() =>
        menuAnimator.SetBool("AnimationComplete", true);

    public void SaveGameSettings()
    {
        PlayerPrefs.SetString("CurrentUsername", usernameInput.text);
        PlayerPrefs.Save();
    }

    public void LoadGameSettings() =>
        usernameInput.text = PlayerPrefs.GetString("CurrentUsername", "Player") ?? GameManager.GetRandomTauntingName();

    public void StartGame() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

    public void SpawnTheFuckingPaper()
    {
        SetMenuState(false);
        SaveGameSettings();
        FindFirstObjectByType<MainMenuZonerDoner>().SpawnExtraSpecialPunchCard();
    }

    public bool FuckingDie()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        return false; // we didn't die.
    }
}

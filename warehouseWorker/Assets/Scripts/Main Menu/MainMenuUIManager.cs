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
    public Toggle difficulty;
    public MainMenuPlayerController playerController;

    private Coroutine animationRoutine;
    private bool isMenuVisible;

    private void Start()
    {
        InitializeUI();
        if (usernameInput) LoadGameSettings();
        else Debug.LogWarning("No username input. ALLOW THE PLAYERS TO PLAY THE GAME YOU DUMB");

        difficulty.isOn = PlayerPrefs.GetInt("extremeDifficulty") > 0;  // TODO: FUC K YOU. CREATE A SAVE FILE! SETTINGS DELETE ALL PLAYERPREFS! FUCK YOU!
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
        playerController.STOPWORKINGIMINUI = isMenuVisible;
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


        animationRoutine = null;
    }

    string playerPrefKey;
    public void SetPlayerPrefKey(string key)
    {
        playerPrefKey = key;
    }

    public void SetPlayerPrefInt(int val)
    {
        PlayerPrefs.SetInt(playerPrefKey, val);
        PlayerPrefs.Save();
    }

    public void SetPlayerPrefBoolToggle(Toggle toggle)
    {
        PlayerPrefs.SetInt(playerPrefKey, toggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    /// <summary> I don't actually use this. It doesn't quite work? Use SelfAnimator thing instead. </summary>
    public void MarkAnimationComplete() =>
        menuAnimator.SetBool("AnimationComplete", true);

    public void SaveGameSettings()
    {
        PlayerPrefs.SetString("CurrentUsername", usernameInput.text);
        PlayerPrefs.Save();
    }

    public void LoadGameSettings()
    {
        var success = LocalizationManager.TryGetVal("default_name", out var name);
        usernameInput.text = PlayerPrefs.GetString("CurrentUsername", success ? name : GameManager.GetRandomTauntingName());

    }

    public void StartGame() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    // TODO: 0 is no longer the main menu. I need to start loading via strings, not ints...

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

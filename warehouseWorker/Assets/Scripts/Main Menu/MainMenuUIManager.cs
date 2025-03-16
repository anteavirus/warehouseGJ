using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Tabs")]
    public GameObject mainMenuTab;
    public GameObject settingsTab;
    public GameObject statisticsTab;
    public GameObject gameSettingsTab;

    [Header("UI Controls")]
    public Button showUIButton;
    public Button hideUIButton;
    public GameObject[] uiElementsToHide;

    [Header("Game Settings")]
    public TMP_InputField usernameInput;
    public TMP_Dropdown difficultyDropdown;

    private void Start()
    {
        // Initialize UI state
        ShowMainMenu();
        UpdateUIVisibility(true);
        LoadGameSettings();
    }

    public void ShowMainMenu()
    {
        SetAllTabsInactive();
        mainMenuTab.SetActive(true);
    }

    public void ShowSettings()
    {
        SetAllTabsInactive();
        settingsTab.SetActive(true);
    }

    public void ShowStatistics()
    {
        SetAllTabsInactive();
        statisticsTab.SetActive(true);
    }

    public void ShowGameSettings()
    {
        SetAllTabsInactive();
        gameSettingsTab.SetActive(true);
    }

    private void SetAllTabsInactive()
    {
        mainMenuTab.SetActive(false);
        settingsTab.SetActive(false);
        statisticsTab.SetActive(false);
        gameSettingsTab.SetActive(false);
    }

    public void UpdateUIVisibility(bool visible)
    {
        foreach (var element in uiElementsToHide)
        {
            element.SetActive(visible);
        }

        showUIButton.gameObject.SetActive(!visible);
        hideUIButton.gameObject.SetActive(visible);
    }

    public void SaveGameSettings()
    {
        PlayerPrefs.SetString("CurrentUsername", usernameInput.text);
        PlayerPrefs.SetInt("GameDifficulty", difficultyDropdown.value);
        PlayerPrefs.Save();
    }

    public void LoadGameSettings()
    {
        usernameInput.text = PlayerPrefs.GetString("CurrentUsername", "Player");
        difficultyDropdown.value = PlayerPrefs.GetInt("GameDifficulty", 0);
    }

    public void StartGame()
    {
        // Load your game scene here
        Debug.Log("Starting game with settings:");
        Debug.Log("Username: " + PlayerPrefs.GetString("CurrentUsername"));
        Debug.Log("Difficulty: " + PlayerPrefs.GetInt("GameDifficulty"));
    }
}

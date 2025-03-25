using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{

    [System.Serializable]
    public class KeyBind
    {
        public string actionName;
        public KeyCode defaultKey;
        [HideInInspector] public KeyCode currentKey;
    }

    [Header("UI References")]
    [SerializeField] private Transform settingsPanelsParent;
    [SerializeField] private List<GameObject> settingsPanels = new List<GameObject>();
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private GameObject rebindPanel;
    [SerializeField] private TMP_Text rebindText;

    [Header("Key Bindings")]
    [SerializeField] private List<KeyBind> keyBinds = new List<KeyBind>();
    [SerializeField] private GameObject keyBindButtonPrefab;
    [SerializeField] private Transform keyBindContainer;

    [Header("Category List")]
    [SerializeField] private GameObject categoryButtonPrefab;
    [SerializeField] private Transform categoryListContent;

    [Header("Mouse Settings")]
    [SerializeField] private Slider mouseSensitivitySlider;

    private Resolution[] resolutions;
    private bool isRebinding;
    private KeyBind currentRebind;

    #region Initialization
    private void Start()
    {
        InitializeMouseSettings();
        InitializeResolutions();
        InitializeQuality();
        InitializeVolume();
        CreateKeyBindUI();
        CreateCategoryButtons();
        LoadSettings();
        ShowFirstPanel();
    }

    private void InitializeMouseSettings()
    {
        mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 100f);
    }

    void InitializeResolutions()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width}x{resolutions[i].height} @ {resolutions[i].refreshRateRatio}Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
    }

    void InitializeQuality()
    {
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
    }

    void InitializeVolume()
    {
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = volumeSlider.value;
    }

    void CreateKeyBindUI()
    {
        foreach (Transform child in keyBindContainer) Destroy(child.gameObject);

        foreach (KeyBind bind in keyBinds)
        {
            GameObject button = Instantiate(keyBindButtonPrefab, keyBindContainer);
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();
            texts[0].text = bind.actionName;
            texts[1].text = bind.currentKey.ToString();

            Button btn = button.GetComponent<Button>();
            btn.onClick.AddListener(() => StartRebinding(bind, texts[1]));
        }
    }

    void CreateCategoryButtons()
    {
        foreach (Transform child in categoryListContent) Destroy(child.gameObject);

        for (int i = 0; i < settingsPanels.Count; i++)
        {
            int index = i;
            GameObject button = Instantiate(categoryButtonPrefab, categoryListContent);

            // Set button text to panel name (remove "Panel" suffix if present)
            string panelName = settingsPanels[i].name.Replace("Panel", "");
            button.GetComponentInChildren<TMP_Text>().text = panelName;

            button.GetComponent<Button>().onClick.AddListener(() => {
                ShowPanel(index);
            });
        }
    }

    void ShowFirstPanel()
    {
        if (settingsPanels.Count > 0)
        {
            foreach (var panel in settingsPanels) panel.SetActive(false);
            settingsPanels[0].SetActive(true);
        }
    }

    #endregion

    #region Panel Management
    public void ShowPanel(int panelIndex)
    {
        for (int i = 0; i < settingsPanels.Count; i++)
        {
            settingsPanels[i].SetActive(i == panelIndex);
        }
    }
    #endregion

    #region Graphics Settings
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
    #endregion

    #region Audio Settings
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }
    #endregion

    #region Key Rebinding
    public void StartRebinding(KeyBind bind, TMP_Text keyText)
    {
        if (isRebinding) return;

        currentRebind = bind;
        rebindPanel.SetActive(true);
        rebindText.text = $"Press any key for: {bind.actionName}";
        StartCoroutine(RebindKey(keyText));
    }

    IEnumerator RebindKey(TMP_Text keyText)
    {
        isRebinding = true;
        yield return new WaitForSeconds(0.2f);

        while (!Input.anyKeyDown) yield return null;

        rebindText.text = "Attempting rebinding... if this is visible, it's possible that I have failed.";
        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(keyCode))
            {
                if (!IsKeyBound(keyCode))
                {
                    currentRebind.currentKey = keyCode;
                    keyText.text = keyCode.ToString();
                    SaveKeyBinds();
                    rebindText.text = "Rebind completed successfully!";
                }
                break;
            }
        }

        rebindPanel.SetActive(false);
        isRebinding = false;
    }



    bool IsKeyBound(KeyCode key)
    {
        foreach (KeyBind bind in keyBinds)
        {
            if (bind.currentKey == key) return true;
        }
        return false;
    }

    void SaveKeyBinds()
    {
        foreach (KeyBind bind in keyBinds)
        {
            PlayerPrefs.SetInt(bind.actionName, (int)bind.currentKey);
        }
    }

    void LoadKeyBinds()
    {
        foreach (KeyBind bind in keyBinds)
        {
            bind.currentKey = (KeyCode)PlayerPrefs.GetInt(bind.actionName, (int)bind.defaultKey);
        }
    }
    #endregion

    #region Save/Load
    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        LoadKeyBinds();
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
    }

    public void ResetToDefault()
    {
        foreach (KeyBind bind in keyBinds)
        {
            bind.currentKey = bind.defaultKey;
        }
        AudioListener.volume = 1f;
        QualitySettings.SetQualityLevel(2);
        PlayerPrefs.DeleteAll();
        CreateKeyBindUI();
    }
    #endregion

    #region Input Access
    public bool GetAction(string actionName)
    {
        KeyBind bind = keyBinds.Find(b => b.actionName == actionName);
        return bind != null && Input.GetKey(bind.currentKey);
    }

    public bool GetActionDown(string actionName)
    {
        KeyBind bind = keyBinds.Find(b => b.actionName == actionName);
        return bind != null && Input.GetKeyDown(bind.currentKey);
    }
    #endregion
    // im stupid
    public void SetMouseSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivity);
    }
}

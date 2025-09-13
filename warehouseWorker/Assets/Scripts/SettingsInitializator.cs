using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;
using System;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

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
    [SerializeField] private GameObject rebindPanel;
    [SerializeField] private TMP_Text rebindText;

    [Header("Audio Settings")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

    [Header("Key Bindings")]
    public List<KeyBind> keyBinds = new List<KeyBind>();
    [SerializeField] private GameObject keyBindButtonPrefab;
    [SerializeField] private Transform keyBindContainer;

    [Header("Category List")]
    [SerializeField] private GameObject categoryButtonPrefab;
    [SerializeField] private Transform categoryListContent;

    [Header("Mouse Settings")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Header("Lighting Settings")]
    public Color defaultEnvironmentLight = UsefulStuffs.ColorFromHex("#060607");
    public Color currentEnvironmentLight;
    public Slider brightnessSlider;

    private Resolution[] resolutions;
    private bool isRebinding;
    private KeyBind currentRebind;

    // Lighting constants
    private const string ENVIRONMENT_LIGHT_KEY = "EnvironmentLightColor";
    private const string BRIGHTNESS_KEY = "EnvironmentLightBrightness";

    bool started = false;

    public void InitializeThyself()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        ForceStart();
    }

    #region Initialization
    private void Awake()
    {
        if (Instance != null && Instance.started) return;
        InitializeThyself();
    }

    public void ForceStart()
    {
        if (Instance.started) return;
        started = true;
        InitializeMouseSettings();
        InitializeResolutions();
        InitializeVolume();
        InitializeLighting();
        LoadSettings();
        CreateKeyBindUI();
        CreateCategoryButtons();
        CreateLanguageButtons();
        ShowFirstPanel();
    }

    private void CreateLanguageButtons()
    {
        if (LocalizationManager.Instance == null) return;
        LocalizationManager.Instance.languageSelectionContent = settingsPanels.Find(i => i.name == "language").transform.Find("Scroll View").Find("Viewport").Find("Content").gameObject;
        LocalizationManager.Instance.Initialize();
    }

    private void Start()
    {
        ForceStart();
    }

    private void InitializeMouseSettings()
    {
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 100f);
        mouseSensitivitySlider.value = sensitivity;
        SetMouseSensitivity(sensitivity);
    }

    private void InitializeLighting()
    {
        // Setup brightness slider
        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);

            float savedBrightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 1f);
            brightnessSlider.value = savedBrightness;
        }

        currentEnvironmentLight = LoadColor(ENVIRONMENT_LIGHT_KEY, defaultEnvironmentLight);

        float currentBrightness = brightnessSlider != null ? brightnessSlider.value : 1f;
        ApplyEnvironmentLight(currentEnvironmentLight, currentBrightness);
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

    void InitializeVolume()
    {
        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        SetVolume(masterSlider, "Master");

        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
        SetVolume(sfxSlider, "SFX");

        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        SetVolume(musicSlider, "Music");
    }

    public string GetKeyDisplay(string actionName)
    {
        var keyBind = keyBinds.Find(b => b.actionName == actionName);
        if (keyBind == null) return "None";

        KeyCode key = keyBind.currentKey != KeyCode.None ? keyBind.currentKey : keyBind.defaultKey;

        return ConvertMouseButtons(key.ToString());
    }

    // TODO: kill whoever moaned about this being an issue.
    private static readonly Dictionary<string, string> mouseButtonTranslations = new Dictionary<string, string>
    {
        {"Mouse0", "ЛКМ"},
        {"Mouse1", "ПКМ"},
        {"Mouse2", "Колесико Мыши"},
    };

    private string ConvertMouseButtons(string keyString)
    {
        return mouseButtonTranslations.TryGetValue(keyString, out string translated) ? translated : keyString;
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

    public void SetQuality(TMP_Dropdown qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex.value);
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetResolution(TMP_Dropdown resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex.value];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetFullscreen(Toggle isFullscreen)
    {
        Screen.fullScreen = isFullscreen.isOn;
    }
    #endregion

    #region Audio Settings
    public void SetVolume(Slider who, string category)
    {
        float volume = who.value;
        SetVolume(volume, category);
    }

    public void SetVolume(float volume, string category)
    {
        float dB = volume > 0.0001f ? 20f * Mathf.Log10(volume) : -80f;

        switch (category)
        {
            case "Master":
                audioMixer.SetFloat("Master", dB);
                PlayerPrefs.SetFloat("MasterVolume", volume);
                break;
            case "SFX":
                audioMixer.SetFloat("SFX", dB);
                PlayerPrefs.SetFloat("SFXVolume", volume);
                break;
            case "Music":
                audioMixer.SetFloat("MUS", dB);
                PlayerPrefs.SetFloat("MusicVolume", volume);
                break;
            default:
                Debug.LogWarning("Invalid audio category: " + category);
                break;
        }
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(Slider who)
    {
        SetVolume(who, "Master");
    }

    public void SetSFXVolume(Slider who)
    {
        SetVolume(who, "SFX");
    }

    public void SetMusicVolume(Slider who)
    {
        SetVolume(who, "Music");
    }
    #endregion

    #region Lighting Settings
    public void OnBrightnessChanged(Slider slider)
    {
        OnBrightnessChanged(slider.value);
    }

    public void OnBrightnessChanged(float brightnessValue)
    {
        // Save brightness setting
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, brightnessValue);
        PlayerPrefs.Save();

        // Apply brightness to current color
        ApplyEnvironmentLight(currentEnvironmentLight, brightnessValue);
    }

    public void SetEnvironmentLight(Color newColor)
    {
        currentEnvironmentLight = newColor;

        // Save color to PlayerPrefs (convert to hex first)
        string hex = UsefulStuffs.ColorToHex(newColor);
        PlayerPrefs.SetString(ENVIRONMENT_LIGHT_KEY, hex);
        PlayerPrefs.Save();

        // Apply with current brightness
        float currentBrightness = brightnessSlider != null ? brightnessSlider.value : 1f;
        ApplyEnvironmentLight(newColor, currentBrightness);
    }

    private Color ApplyBrightnessToDarkColor(Color color, float brightness)
    {
        // For very dark colors, use a different approach
        if (UsefulStuffs.CalculateLuminance(color) < 0.1f)
        {
            // Convert to HSV for better control
            Color.RGBToHSV(color, out float h, out float s, out float v);

            // Adjust value (brightness) component
            v = Mathf.Clamp(v * brightness, 0f, 1f);

            // For very dark colors, also reduce saturation as brightness increases
            if (brightness > 1.5f)
            {
                s = Mathf.Clamp(s / (brightness * 0.8f), 0f, 1f);
            }

            return Color.HSVToRGB(h, s, v);
        }
        else
        {
            // For brighter colors, use standard multiplication
            return new Color(
                Mathf.Clamp(color.r * brightness, 0f, 1f),
                Mathf.Clamp(color.g * brightness, 0f, 1f),
                Mathf.Clamp(color.b * brightness, 0f, 1f),
                color.a
            );
        }
    }

    public void SetEnvironmentLightFromHex(string hexColor)
    {
        Color color = UsefulStuffs.ColorFromHex(hexColor);
        SetEnvironmentLight(color);
    }

    public void ResetToDefaultLight()
    {
        SetEnvironmentLight(defaultEnvironmentLight);
        if (brightnessSlider != null)
        {
            brightnessSlider.value = 1f;
        }
    }

    private void ApplyEnvironmentLight(Color color, float brightness)
    {
        Color finalColor = ApplyBrightnessToDarkColor(color, brightness);

        RenderSettings.ambientSkyColor = finalColor;
    }

    private Color LoadColor(string key, Color defaultColor)
    {
        if (PlayerPrefs.HasKey(key))
        {
            string hex = PlayerPrefs.GetString(key);
            return UsefulStuffs.ColorFromHex(hex);
        }
        return defaultColor;
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

    public void LoadSettings()
    {
        LoadKeyBinds();
        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", .5f);
        SetVolume(masterSlider.value, "Master");

        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", .5f);
        SetVolume(sfxSlider.value, "SFX");

        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", .5f);
        SetVolume(musicSlider.value, "Music");

        mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 100f);
        SetMouseSensitivity(mouseSensitivitySlider.value);

        float brightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 1f);
        if (brightnessSlider != null)
        {
            brightnessSlider.value = brightness;
        }
        currentEnvironmentLight = LoadColor(ENVIRONMENT_LIGHT_KEY, defaultEnvironmentLight);
        ApplyEnvironmentLight(currentEnvironmentLight, brightness);

        PlayerPrefs.Save();
    }

    public void ResetToDefault()
    {
        foreach (KeyBind bind in keyBinds)
        {
            bind.currentKey = bind.defaultKey;
        }

        AudioListener.volume = 1f;
        SetVolume(.5f, "Master");
        SetVolume(.5f, "SFX");
        SetVolume(.5f, "Music");

        mouseSensitivitySlider.value = 100f;
        SetMouseSensitivity(100f);

        // Reset lighting settings
        ResetToDefaultLight();

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

    public bool GetActionUp(string actionName)
    {
        KeyBind bind = keyBinds.Find(b => b.actionName == actionName);
        return bind != null && Input.GetKeyUp(bind.currentKey);
    }
    #endregion

    #region Mouse Sensitivity
    public void SetMouseSensitivity(Slider slider)
    {
        SetMouseSensitivity(slider.value);
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivity);
        PlayerPrefs.Save();
    }
    #endregion

    // Save settings when application quits
    private void OnApplicationQuit()
    {
        SaveSettings();
    }
}
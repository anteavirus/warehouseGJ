using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;
using System;
using System.Linq;
using UnityEngine.Rendering;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    Settings settings = new();
    FileDataManipulator settingsFileManip;

    [System.Serializable]
    public class KeyBind
    {
        public string actionName;
        public KeyCode defaultKey;
        public KeyCode currentKey;
    }

    [System.Serializable]
    public class Settings
    {
        // Graphics Settings
        public int resolutionIndex = 0;
        public int qualityLevel = 2;
        public bool fullscreen = true;

        // Audio Settings
        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public float musicVolume = 1f;

        // Input Settings
        public float mouseSensitivity = 100f;

        // Lighting Settings
        public string environmentLightColor = "#060607";
        public float brightness = 1f;

        public List<KeyBind> keyBindings = new();
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
            return;
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
        if (Instance == null) return;  
        // theoretically, shouldn't be called, as we instantly run awake -> either instance is this OR it destroys itself, which means there is an instance.
        // in practice, memleak, prolly.
        if (Instance.started) return;
        started = true;
        settingsFileManip = FileDataManipulator.ForPersistentDataPath(settings, new string[1] { "settings.json" });
        settings = settingsFileManip.LoadData<Settings>();

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
        float sensitivity = settings.mouseSensitivity;
        mouseSensitivitySlider.value = sensitivity;
        SetMouseSensitivity(sensitivity);
    }

    private void InitializeLighting()
    {
        // Setup brightness slider
        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);

            float savedBrightness = settings.brightness;
            brightnessSlider.value = savedBrightness;
        }

        currentEnvironmentLight = UsefulStuffs.ColorFromHex(string.IsNullOrEmpty(settings.environmentLightColor) ? UsefulStuffs.ColorToHex(defaultEnvironmentLight) : settings.environmentLightColor);
        // bloatcode...

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
        masterSlider.value = settings.masterVolume;
        SetVolume(masterSlider, "Master");

        sfxSlider.value = settings.sfxVolume;
        SetVolume(sfxSlider, "SFX");

        musicSlider.value = settings.musicVolume;
        SetVolume(musicSlider, "Music");
    }

    public string GetKeyDisplay(string actionName)
    {
        var keyBind = keyBinds.Find(b => b.actionName == actionName);
        if (keyBind == null) return "None";

        KeyCode key = keyBind.currentKey != KeyCode.None ? keyBind.currentKey : keyBind.defaultKey;

        string keyStr = key.ToString();
        if (keyStr.Length < 2) return keyStr;

        string localizationKey = GetKeyLocalizationKey(key);
        if (LocalizationManager.TryGetVal(localizationKey, out var localizedKey))
            return localizedKey;

        return keyStr;
    }

    private string GetKeyLocalizationKey(KeyCode key)
    {
        string keyName = key.ToString();

        if (keyName.StartsWith("Mouse"))
            if (int.TryParse(keyName.Split("Mouse")[1], out int mouseVal))  // NOTE: [0] actually contains a null value 
            {
                if (mouseVal >= 0 && mouseVal <= 2)
                    return $"key_mouse_{mouseVal}";
                else
                    return "key_mouse";
            }

        return $"key_{keyName.ToLower()}";
    }

    private string GetActionNameLocalizationKey(string actionName)
    {
        return $"action_{actionName.ToLower().Replace(" ", "_")}";
    }

    void CreateKeyBindUI()
    {
        foreach (Transform child in keyBindContainer)
            Destroy(child.gameObject);

        foreach (KeyBind bind in keyBinds)
        {
            GameObject button = Instantiate(keyBindButtonPrefab, keyBindContainer);
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();

            string actionKey = GetActionNameLocalizationKey(bind.actionName);
            if (LocalizationManager.TryGetVal(actionKey, out var localizedAction))
            {
                texts[0].text = localizedAction;
            }
            else
            {
                texts[0].text = bind.actionName;
            }

            var actionNameText = texts[0].gameObject.AddComponent<LocalizedText>();
            actionNameText.localizationKey = actionKey;

            texts[1].text = GetKeyDisplay(bind.actionName);

            if (bind.currentKey.ToString().Length > 1)
            {
                var keyText = texts[1].gameObject.AddComponent<LocalizedText>();
                keyText.localizationKey = GetKeyLocalizationKey(bind.currentKey != KeyCode.None ? bind.currentKey : bind.defaultKey);
            }

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
        settings.qualityLevel = qualityIndex.value;
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        settings.qualityLevel = qualityIndex;
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        settings.resolutionIndex = resolutionIndex;
    }

    public void SetResolution(TMP_Dropdown resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex.value];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        settings.resolutionIndex = resolutionIndex.value;
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        settings.fullscreen = isFullscreen;
    }

    public void SetFullscreen(Toggle isFullscreen)
    {
        Screen.fullScreen = isFullscreen.isOn;
        settings.fullscreen = isFullscreen.isOn;
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
                settings.masterVolume = volume;
                break;
            case "SFX":
                audioMixer.SetFloat("SFX", dB);
                settings.sfxVolume = volume;
                break;
            case "Music":
                audioMixer.SetFloat("MUS", dB);
                settings.musicVolume = volume;
                break;
            default:
                Debug.LogWarning("Invalid audio category: " + category);
                break;
        }
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
        settings.brightness = brightnessValue;

        // Apply brightness to current color
        ApplyEnvironmentLight(currentEnvironmentLight, brightnessValue);
    }

    public void SetEnvironmentLight(Color newColor)
    {
        currentEnvironmentLight = newColor;

        string hex = UsefulStuffs.ColorToHex(newColor);
        settings.environmentLightColor = hex;

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
        yield return new WaitForSecondsRealtime(0.2f);

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
        settings.keyBindings.Clear();
        settings.keyBindings.AddRange(keyBinds);
    }

    void LoadKeyBinds()
    {
        foreach (KeyBind bind in keyBinds)
        {
            var currentKeyBindInSettings = settings.keyBindings.Find(i => i.actionName == bind.actionName);
            if (currentKeyBindInSettings != null)
                bind.currentKey = currentKeyBindInSettings.currentKey == KeyCode.None ? bind.defaultKey : currentKeyBindInSettings.currentKey;
            else
                bind.currentKey = bind.defaultKey;
        }
    }
    #endregion

    #region Save/Load
    public void SaveSettings()  // this is WRITING DATA on DISK! as in: do this last!
    {
        settingsFileManip.SaveData(settings);
    }

    public void LoadSettings()
    {
        LoadKeyBinds();

        mouseSensitivitySlider.value = settings.mouseSensitivity;
        SetMouseSensitivity(mouseSensitivitySlider.value);

        float brightness = settings.brightness;
        if (brightnessSlider != null)
        {
            brightnessSlider.value = brightness;
        }
        
        currentEnvironmentLight = UsefulStuffs.ColorFromHex(string.IsNullOrEmpty(settings.environmentLightColor) ? UsefulStuffs.ColorToHex(defaultEnvironmentLight) : settings.environmentLightColor);
        // bloatcode! I'll just make the computer the extra work for now, rather than making me do it myself. 

        ApplyEnvironmentLight(currentEnvironmentLight, brightness);
    }

    public void ResetToDefault()
    {
        settings = new Settings();
        foreach (KeyBind bind in keyBinds)
        {
            bind.currentKey = bind.defaultKey;
            settings.keyBindings.Add(bind);
        }

        masterSlider.value = 1f;
        sfxSlider.value = 1f;
        musicSlider.value = 1f;

        AudioListener.volume = 1f;
        SetVolume(1f, "Master");
        SetVolume(1f, "SFX");
        SetVolume(1f, "Music");

        mouseSensitivitySlider.value = 100f;
        SetMouseSensitivity(100f);

        // Reset lighting settings
        ResetToDefaultLight();

        QualitySettings.SetQualityLevel(2); 
        // Do i want to make a graphics menu..? not really... but it is kinda necessary, no..? actually, downsizing the window is fine enough...

        CreateKeyBindUI();

        SaveSettings();
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
        settings.mouseSensitivity = sensitivity;
    }
    #endregion

    // Save settings when application quits
    private void OnApplicationQuit()
    {
        SaveSettings();
    }
}
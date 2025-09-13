using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.IO;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    public GameObject languageSelectionButtonPrefab;
    public GameObject languageSelectionContent;

    [System.Serializable]
    class LanguageData
    {
        public string languageCode;
        public string languageName;
        public List<SerializableDictionary<string, string>.SerializableKeyValuePair> translations;
    }

    readonly List<LanguageData> languages = new();
    public string defaultLanguage = "en";
    public string currentLanguage;

    public event Action OnLanguageChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
            Instance.Initialize();
        }
    }

    public void Initialize()
    {
        currentLanguage = PlayerPrefs.GetString("SelectedLanguage", defaultLanguage);   // TODO: HOW MANY TIMES DO I HAVE TO TEACH ME THIS LESSON?
        LoadLanguages();
    }

    void LoadLanguages()
    {
        if (languageSelectionContent == null)
            languageSelectionContent = FindObjectOfType<ScrollRect>()?.transform.Find("Viewport")?.Find("Content")?.gameObject;
        // i failed as a coder

        LoadLanguagesFromResources();
        LoadLanguagesFromStreamingAssets();

        if (languages.Count == 0)
        {
            Debug.LogError("No language files found. Good job!");
        }

        CreateLanguageButtons();
    }

    private void CreateLanguageButtons()
    {
        if (languageSelectionContent == null)
        {
            Debug.LogError("Language selection content is not assigned!");
            return;
        }

        foreach (Transform child in languageSelectionContent.transform)
        {
            Destroy(child.gameObject);
        }

        foreach (var lang in languages)
        {
            var button = Instantiate(languageSelectionButtonPrefab, languageSelectionContent.transform);

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = lang.languageName;
            }

            if (button.TryGetComponent<Button>(out var buttonComponent))
            {
                buttonComponent.onClick.AddListener(() =>
                {
                    SetLanguage(lang.languageCode);
                    Debug.Log($"Selected language: {lang.languageName}");

                    if (SceneManager.GetActiveScene().name == "LanguageSelection") LoadScene("Main Menu", null);
                });
            }
        }

        if (languages.Count == 0)
        {
            GameObject warningText = new("WarningText");
            warningText.transform.SetParent(languageSelectionContent.transform);
            TextMeshProUGUI textComponent = warningText.AddComponent<TextMeshProUGUI>();
            textComponent.text = "No languages available. I have no idea how this happened...";
            textComponent.color = Color.red;
            textComponent.alignment = TextAlignmentOptions.Center;
        }
    }

    public void LoadScene(string name, object labubu)
    {
        if (labubu == null)
        {
            LocalizationManager.Instance.LoadScene(name, this);
            return;
        }
        SceneManager.LoadScene(name, LoadSceneMode.Single);
    }

    void LoadLanguagesFromResources()
    {
        TextAsset[] languageFiles = Resources.LoadAll<TextAsset>("Localization");

        foreach (TextAsset file in languageFiles)
        {
            try
            {
                LanguageData languageData = JsonUtility.FromJson<LanguageData>(file.text);
                if (languageData != null && !LanguageExists(languageData.languageCode))
                {
                    languages.Add(languageData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load language file {file.name}: {e.Message}");
            }
        }
    }

    void LoadLanguagesFromStreamingAssets()
    {
        string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "Localization");

        if (!Directory.Exists(streamingAssetsPath))
        {
            Directory.CreateDirectory(streamingAssetsPath);
            return;
        }

        string[] languageFiles = Directory.GetFiles(streamingAssetsPath, "*.json");

        foreach (string filePath in languageFiles)
        {
            try
            {
                string jsonData = File.ReadAllText(filePath);
                LanguageData languageData = JsonUtility.FromJson<LanguageData>(jsonData);

                if (languageData != null)
                {
                    LanguageData existingLang = languages.Find(lang => lang.languageCode == languageData.languageCode);
                    if (existingLang != null)
                    {
                        languages.Remove(existingLang);
                        Debug.Log($"Overriding hardcoded language {languageData.languageCode} with custom version");
                    }

                    languages.Add(languageData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load custom language file {filePath}: {e.Message}");
            }
        }
    }

    bool LanguageExists(string languageCode)
    {
        return languages.Exists(lang => lang.languageCode == languageCode);
    }

    public string GetTranslation(string key)
    {
        LanguageData currentLangData = languages.Find(lang => lang.languageCode == currentLanguage);

        if (currentLangData != null && currentLangData.translations != null)
        {
            var translation = currentLangData.translations.Find(item => item.Key == key);
            if (translation != null)
            {
                return translation.Value;
            }
        }

        LanguageData defaultLangData = languages.Find(lang => lang.languageCode == defaultLanguage);
        if (defaultLangData != null && defaultLangData.translations != null)
        {
            var translation = defaultLangData.translations.Find(item => item.Key == key);
            if (translation != null)
            {
                return translation.Value;
            }
        }

        Debug.LogWarning($"Translation key '{key}' not found in {currentLanguage} nor {defaultLanguage} language.");
        return $"#{key}#";
    }

    public void SetLanguage(string languageCode)
    {
        if (languages.Exists(lang => lang.languageCode == languageCode))
        {
            currentLanguage = languageCode;
            PlayerPrefs.SetString("SelectedLanguage", languageCode);
            PlayerPrefs.Save();

            OnLanguageChanged?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Language '{languageCode}' not available.");
        }
    }

    public List<string> GetAvailableLanguages()
    {
        List<string> availableLanguages = new();
        foreach (LanguageData language in languages)
        {
            availableLanguages.Add(language.languageCode);
        }
        return availableLanguages;
    }

    public bool HasKey(string key)
    {
        foreach (LanguageData language in languages)
        {
            if (language.translations != null && language.translations != null)
            {
                if (language.translations.Exists(item => item.Key == key))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static string Get(string key)
    {
        if (Instance != null)
        {
            return Instance.GetTranslation(key);
        }
        return $"#{key}#";
    }

    public static bool TryGetVal(string key, out string value)
    {
        if (Instance != null)
        {
            value = Instance.GetTranslation(key);
            return value != $"#{key}#";
        }
        value = $"#{key}#";
        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.IO;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LocalizationManager : GenericManager<LocalizationManager>
{
    public GameObject languageSelectionButtonPrefab;
    public GameObject languageSelectionContent;

    [System.Serializable]
    class LanguageData
    {
        /// <summary>
        /// "Ключ" языка чтобы отличать AU от TI и ST
        /// </summary>
            public string languageCode;
        
        /// <summary>
        /// Название самого языка. Можем назвать "SIGMA CREEPER [С ОГУРЦОМ]" если захотим
        /// </summary>
            public string languageName;
        
        /// <summary>
        /// "Ключ" | Перевод, буковки все. 
        /// </summary>
            public List<SerializableDictionary<string, string>.SerializableKeyValuePair> translations;
    }

    readonly List<LanguageData> languages = new();
    public string defaultLanguage = "en";  // american goy. или boy оба подходят прекрасно
    public string currentLanguage;

    public event Action OnLanguageChanged;

    public override void Initialize()
    {
        base.Initialize();
        currentLanguage = PlayerPrefs.GetString("SelectedLanguage", defaultLanguage);   // TODO: желательно в префы совать префы, и честно это сюда подходит. но я не уверен.
        LoadLanguages();
    }

    void LoadLanguages()
    {
        if (languageSelectionContent == null)
            languageSelectionContent = FindObjectOfType<LanguageSelectorMarker>(true)?.gameObject;  // ищем везде как можем

        if (languageSelectionContent == null)
            Debug.LogError("Ну что же. Не нашли маркер. Пошли вы все в лес за грибами");

        LoadLanguagesFromResources();
        LoadLanguagesFromStreamingAssets();

        if (languages.Count == 0)
        {
            Debug.LogError("Не были найдены файлы языков. круто");
        }

        CreateLanguageButtons();
    }

    private void CreateLanguageButtons()
    {
        if (languageSelectionContent == null)
        {
            Debug.LogError("Не было дезигнировано поле для заполнения выборами языков! я про контент бтв");
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
                    Debug.Log($"Выбран язык: {lang.languageName}");
                    
                    // по рофлу кинем игрока в главное меню если мы в выборе языка. при нажатии любого из языков. сука а идея-то хуйня
                    if (SceneManager.GetActiveScene().name == "LanguageSelection") 
                        LoadScene("Main Menu", null);
                });
            }
        }

        if (languages.Count == 0)
        {
            GameObject warningText = new("WarningText");
            warningText.transform.SetParent(languageSelectionContent.transform);
            TextMeshProUGUI textComponent = warningText.AddComponent<TextMeshProUGUI>();
            textComponent.text = "Языки не были доступны. Я не знаю как это случилось... And sorry i cant be bothered to add localization for this message im busy doing jackshit with tons of work on my back";
            textComponent.color = Color.red;
            textComponent.alignment = TextAlignmentOptions.Center;
        }
    }


    // это вроде чтобы подгрузить что-то??? но это же не корутина и не асинхр. метод так что он тупо запустится в любом случае.
    // ну и нахуй я это писал так.
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
                    Debug.Log($"Language loaded {languageData.languageName}");
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
                        Debug.Log($"Заменяю захардкоденный язык '{languageData.languageCode}' на пользовательский. жду шайбы по лбу");
                        languageData.languageName += " [CUSTOM]";
                    }

                    languages.Add(languageData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Не удалось подгрузить файл {filePath}: {e.Message}");
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

        Debug.LogWarning($"Ключ перевода '{key}' не найден ни в языке '{currentLanguage}' ни в '{defaultLanguage}'. Используем ужасный ключик '#{key}#'...");
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
            Debug.LogWarning($"Язык '{languageCode}' не существует. Как минимум не был зарегистрирован к этому моменту.");
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

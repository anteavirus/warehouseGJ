using UnityEngine;
using TMPro;
using System;

public class LocalizedText : MonoBehaviour
{
    public LocalizedText(string key, string text = "Despacito")
    {
        localizationKey = key;
        this.text = text;
    }

    public string localizationKey;
    public string text;

    private TMP_Text tmpComponent;

    void Start()
    {
        tmpComponent = GetComponent<TMP_Text>();

        UpdateText();

        LocalizationManager.Instance.OnLanguageChanged += UpdateText;
    }

    void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= UpdateText;
        }
    }

    public void UpdateText()
    {
        bool success = LocalizationManager.TryGetVal(localizationKey, out var translatedText);
        // TODO: if i didn't rework this boilerplate shit, kill me
        if (tmpComponent != null)
        {
            text = tmpComponent.text;
            if (success)
            {
                text = tmpComponent.text = translatedText;
            }
        }
        if (success)
        {
            text = translatedText;
        }
    }
}
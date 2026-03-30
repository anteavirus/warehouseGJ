using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Class to realize a menu that would just enable/disable other parts of the screen which we were to design beforehand assuming it's the main only enabled
/// </summary>
public class ShittyFuckingHelpScreen : MonoBehaviour
{
    /// <summary>
    /// The parts the player can select
    /// </summary>
    public GameObject[] answers;
    public TextMeshProUGUI title;
    public GameObject questionSelection;
    public GameObject simpleButtonPrefab;

    private void Start()
    {
        for (int i = 0; i < answers.Length; i++)
        {
            var answer = answers[i];
            var localized = answer.GetComponent<LocalizedText>();
            localized.UpdateText();

            answer.name = localized.text;
            var obj = Instantiate(simpleButtonPrefab, questionSelection.transform);
            obj.GetComponentInChildren<TextMeshProUGUI>().text = answer.name;

            // TODO: probably needs localization appended to a button's text and have that name be pulled from the localization file instead. Incase player switches from Mandarin to Finnish

            int currentIndex = i;
            obj.GetComponent<Button>().onClick.AddListener(() =>
            {
                ShowMenu(currentIndex);
            });
        }
    }


    public void ShowMenu(int index)
    {
        title.text = answers[index].name;
        foreach (var item in answers)
        {
            item.SetActive(false);
        }
        answers[index].SetActive(true);
    }
}

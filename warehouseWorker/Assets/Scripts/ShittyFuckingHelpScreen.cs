using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShittyFuckingHelpScreen : MonoBehaviour
{
    public GameObject[] answers;
    public TextMeshProUGUI title;
    public GameObject questionSelection;
    public GameObject simpleButtonPrefab;

    private void Start()
    {
        for (int i = 0; i < answers.Length; i++)
        {
            var answer = answers[i];
            var obj = Instantiate(simpleButtonPrefab, questionSelection.transform);
            obj.GetComponentInChildren<TextMeshProUGUI>().text = answer.name;

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

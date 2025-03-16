using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    [System.Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        public string description;
        public bool unlocked;
    }

    public List<Achievement> achievements = new List<Achievement>();
    public Transform achievementsContainer;
    public GameObject achievementPrefab;

    private void Start()
    {
        LoadAchievements();
        PopulateUI();
    }

    private void LoadAchievements()
    {
        foreach (var achievement in achievements)
        {
            achievement.unlocked = PlayerPrefs.GetInt("ACH_" + achievement.id, 0) == 1;
        }
    }

    private void PopulateUI()
    {
        foreach (Transform child in achievementsContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var achievement in achievements)
        {
            GameObject obj = Instantiate(achievementPrefab, achievementsContainer);
            TextMeshProUGUI[] texts = obj.GetComponentsInChildren<TextMeshProUGUI>();
            texts[0].text = achievement.title;
            texts[1].text = achievement.description;
            texts[2].text = achievement.unlocked ? "Unlocked!" : "Locked";
            texts[2].color = achievement.unlocked ? Color.green : Color.red;
        }
    }

    public void UnlockAchievement(string id)
    {
        Achievement achievement = achievements.Find(a => a.id == id);
        if (achievement != null && !achievement.unlocked)
        {
            achievement.unlocked = true;
            PlayerPrefs.SetInt("ACH_" + id, 1);
            PopulateUI();
        }
    }
}

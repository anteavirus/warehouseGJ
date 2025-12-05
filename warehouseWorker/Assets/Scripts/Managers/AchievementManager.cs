using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AchievementManager : GenericManager<AchievementManager>
{
    [System.Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        public string description;
        public bool unlocked;
    }

    [System.Serializable]
    public class Achievements
    {
        public List<Achievement> ach_list;
        // TODO: refine. looks naked
    }

    FileDataManipulator achievementsManip;
    Achievements achievements = new();
    public Transform achievementsContainer;
    public GameObject achievementPrefab;

    public override void Initialize()
    {
        achievementsManip = FileDataManipulator.ForPersistentDataPath(achievements, new string[1] { "achievements.sv" });
        achievements = (Achievements) achievementsManip.LoadData();  // TODO: needs to load achievements from somewhere. perhaps creating a new "localization" folder is fine.
        Debug.LogWarning("I should be properly created. Currently I technically do *some* work, but this isn't enough I believe.");
        return;
        PopulateUI();
    }

    private void PopulateUI()
    {
        foreach (Transform child in achievementsContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var achievement in achievements.ach_list)
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
        Achievement achievement = achievements.ach_list.Find(a => a.id == id);
        if (achievement != null && !achievement.unlocked)
        {
            achievement.unlocked = true;
            PopulateUI();
            // TODO: save.
        }
    }
}

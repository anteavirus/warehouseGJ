using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool setdownItem;
    public bool startGame;
    [SerializeField] TextMeshProUGUI scoreUI;
    [SerializeField] Image timerUI;

    [Tooltip("I will kill you if you put something that doesn't have an Item Component here.")]
    [SerializeField] List<GameObject> items = new List<GameObject>();

    [Tooltip("Spawn box, user must unbox the box. Then they bring wherever they need to.")]
    [SerializeField] GameObject box;
    float timer = 30;
    static readonly float maxTimer = 30;
    int score = 0;

    float progressTimer;

    float currentTime = 0;

    float eventTimer = 60;
    [SerializeField] List<GameObject> eventList = new List<GameObject>();
    private Event currentEvent;

    public Transform spawnPosition;

    [Range(0, 10), SerializeField]
    float randomSpawnIntervalMax = 1;

    [Range(0, 10), SerializeField]
    float timerRestore = 1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartGame()
    {
        startGame = true;
        if (items.Count > 0) SpawnItem();
    }

    public void AddScore(int amount, bool resetTimer = true)
    {
        this.score += amount;
        scoreUI.text = this.score.ToString();
        if (resetTimer)
        {
            setdownItem = true;

            StartCoroutine(SpawnItemAfterDelay());
        }
    }

    public void ResetTimer()
    {
        if (setdownItem)
        {
            timer = Mathf.Clamp(timer + timerRestore, 0, maxTimer);
            setdownItem = false;
        }
    }

    IEnumerator SpawnItemAfterDelay()
    {
        yield return new WaitForSeconds(Random.Range(0, randomSpawnIntervalMax));
        SpawnItem();
    }

    void SpawnItem()
    {
        int randomIndex = Random.Range(0, items.Count);
        var box = Instantiate(this.box, spawnPosition.transform.position, Quaternion.identity);
        box.GetComponent<Box>().containedItem = items[randomIndex];
    }

    void Update()
    {
        if (!startGame) return;

        timer -= Time.deltaTime;
        currentTime += Time.deltaTime;

        progressTimer = timer / maxTimer;
        timerUI.fillAmount = progressTimer;

        // Bad!
        var color = timerUI.color;
        color.a = progressTimer;
        timerUI.color = color;

        if (timer < 0 || score < 0)
        {
            GameOver();
        }

        if (currentTime >= eventTimer)
        {
            StartRandomEvent();
            currentTime = 0;
        }

        if (currentEvent != null && currentEvent.isActive)
        {
            currentEvent.UpdateEvent();
        }
    }

    void StartRandomEvent()
    {
        if (eventList.Count == 0) return;

        if (currentEvent != null)
        {
            currentEvent.EndEvent();
        }

        int randomIndex = Random.Range(0, eventList.Count);
        currentEvent = eventList[randomIndex].GetComponent<Event>();
        currentEvent.StartEvent();

        StartCoroutine(EndEventAfterDuration(currentEvent));
    }


    IEnumerator EndEventAfterDuration(Event evt)
    {
        yield return new WaitForSeconds(evt.duration);
        if (currentEvent == evt)
        {
            evt.EndEvent();
            currentEvent = null;
        }
    }

    void GameOver()
    {
        LeaderboardWrapper leaderboard = LoadLeaderboard();
        LeaderboardEntry newEntry = CreateLeaderboardEntry();

        bool added = false;
        for (int i = 0; i < leaderboard.entries.Count; i++)
        {
            if (newEntry.score >= leaderboard.entries[i].score)
            {
                leaderboard.entries.Insert(i, newEntry);
                added = true;
                break;
            }
        }
        if (!added) leaderboard.entries.Add(newEntry);

        if (leaderboard.entries.Count > 10)
        {
            leaderboard.entries = leaderboard.entries.GetRange(0, 10);
        }

        SaveLeaderboard(leaderboard);
        SceneManager.LoadScene(0);
    }

    LeaderboardEntry CreateLeaderboardEntry()
    {
        string username = PlayerPrefs.GetString("CurrentUsername", "");
        if (string.IsNullOrEmpty(username))
        {
            username = GetRandomTauntingName();
        }

        return new LeaderboardEntry
        {
            name = username,
            score = this.score
        };
    }

    string GetRandomTauntingName()
    {
        string[] tauntingNames = { 
            "OofEnthusiast", "SweatySocks", "Bunnyhopper", "FumbleChamp", "ConfettiCannon", 
            "PotatoAim", "ProSK8R", "LootGnoblin", "CertifiedDerp", "ParticipationPrize"
        };

        return tauntingNames[Random.Range(0, tauntingNames.Length)];
    }

    LeaderboardWrapper LoadLeaderboard()
    {
        string json = PlayerPrefs.GetString("Leaderboard", "");
        if (!string.IsNullOrEmpty(json))
        {
            return JsonUtility.FromJson<LeaderboardWrapper>(json);
        }
        return new LeaderboardWrapper();
    }

    static void SaveLeaderboard(LeaderboardWrapper leaderboard)
    {
        string json = JsonUtility.ToJson(leaderboard);
        PlayerPrefs.SetString("Leaderboard", json);
        PlayerPrefs.Save();
    }

    public static void CreateLeaderBoard()
    {
        if (PlayerPrefs.HasKey("Leaderboard")) return;

        LeaderboardWrapper wrapper = LoadRandomLeaderboardTemplate();

        if (wrapper == null || wrapper.entries.Count == 0)
        {
            CreateDefaultLeaderboard(ref wrapper);
        }

        wrapper.entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (wrapper.entries.Count > 10)
        {
            wrapper.entries = wrapper.entries.GetRange(0, 10);
        }

        SaveLeaderboard(wrapper);
    }

    static LeaderboardWrapper LoadRandomLeaderboardTemplate()
    {
        TextAsset[] jsonFiles = Resources.LoadAll<TextAsset>("Leaderboards");
        if (jsonFiles == null || jsonFiles.Length == 0) return null;

        List<LeaderboardWrapper> validTemplates = new List<LeaderboardWrapper>();
        foreach (TextAsset file in jsonFiles)
        {
            try
            {
                LeaderboardWrapper template = JsonUtility.FromJson<LeaderboardWrapper>(file.text);
                if (template != null && template.entries.Count > 0)
                {
                    validTemplates.Add(template);
                }
            }
            catch
            {
                Debug.LogWarning($"Failed to parse leaderboard template: {file.name}");
            }
        }

        if (validTemplates.Count == 0) return null;
        return validTemplates[Random.Range(0, validTemplates.Count)];
    }

    static void CreateDefaultLeaderboard(ref LeaderboardWrapper wrapper)
    {
        wrapper = new LeaderboardWrapper();
        wrapper.entries.AddRange(new List<LeaderboardEntry> {
            new() { name = "ProPaneGamer", score = 6969 },
            new() { name = "CheesePowered", score = 5000 },
            new() { name = "GoofyGooberYeah", score = 2500 },
            new() { name = "NoobLooper", score = 1000 },
            new() { name = "NewbCake", score = 500 },
            new() { name = "LootGoblin", score = 250 },
            new() { name = "BSauce", score = 100 },
            new() { name = "ConfettiMaker", score = -69 },
            new() { name = "BackflipKing", score = -420 },
            new() { name = "TeamSnack", score = -1000 }
        });
    }
}

[System.Serializable]
public class LeaderboardEntry
{
    public string name;
    public int score;
}

[System.Serializable]
public class LeaderboardWrapper
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}
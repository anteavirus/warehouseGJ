using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // Game State
    public bool gameStarted;
    public bool setdownItem;
    internal int levelSeed = 0;
    public int score = 0;

    // Manager References
    [Header("Manager References")]
    public ShelvesStockManager shelvesStockManager;
    public OrdersManager ordersManager;
    public GenericTimer timer;
    private AudioSource audioSource;

    // UI Elements
    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI scoreUI;
    [SerializeField] public Image timerUI;
    [SerializeField] TextMeshProUGUI orderListUI;
    [SerializeField] Image difficultyImage;

    // Items & Templates
    [Header("Items")]
    [Tooltip("I will kill you if you put something that doesn't have an Item Component here.")]
    [SerializeField] List<GameObject> items = new();
    public List<Item> itemTemplates = new();

    // Spawning
    [Header("Spawning")]
    public Transform blackHoleSpawnPosition;

    // Audio
    [Header("Audio")]
    public AudioMixerGroup sfx;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    // Game Mechanics
    [Header("Game Objects")]
    public GameObject talkingDeliveryItem;

    // Difficulty Settings
    [Header("Difficulty Settings")]
    [SerializeField] AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] public float minimalDifficulty = 2, maximumDifficulty = 3;
    [SerializeField] float maxDifficultyTime = 120f;
    private float totalGameTime;
    private float currentDifficulty;

    // Events System
    [Header("Events System")]
    [SerializeField] List<GameObject> eventList = new List<GameObject>();
    public List<Event> activeEvents = new List<Event>();
    [SerializeField] float minRandomTimeEventDecrease = 1, maxRandomTimeEventDecrease = 15;
    private float currentEventTime = 0;
    [SerializeField] private float eventTimer = 60;
    private float selectedRandomTimeEventDecrease = 0;

    // Leaderboard
    [Header("Leaderboard")]
    public LeaderboardEntry leaderboardEntry;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.Log("Dupe game manager; killed.");
            Destroy(gameObject);
        }

        levelSeed = Random.Range(0, 1969);
        InitializeItemTemplates();
        InitializeAudio();
        InitializeManagers();
    }

    void InitializeItemTemplates()
    {
        var parent = new GameObject("[Template]s Parent");
        foreach (var item in items)
        {
            var obj = Instantiate(item);
            var localname = obj.GetComponent<LocalizedText>();
            localname.UpdateText();
            obj.name = localname.text;
            obj.transform.parent = parent.transform;
            var itemComp = obj.GetComponent<Item>();
            itemComp.mixerGroup = sfx;
            itemTemplates.Add(itemComp);
            obj.SetActive(false);
        }
    }

    void InitializeAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void InitializeManagers()
    {
        if (shelvesStockManager != null)
        {
            shelvesStockManager.Initialize(this);
            shelvesStockManager.Work();
        }

        if (ordersManager != null)
        {
            ordersManager.Initialize(this);
        }

        if (timer != null)
        {
            timer.Initialize(this);
        }
    }

    void Update()
    {
        if (!gameStarted) return;

        UpdateGameTime();
        UpdateDifficulty();
        UpdateEvents();

        if (timer != null && timer.enabledTimer) timer.UpdateTimer();
        if (ordersManager != null) ordersManager.UpdateOrders();

        CheckForGameOver();
    }

    void UpdateGameTime()
    {
        totalGameTime += Time.deltaTime;
    }

    void UpdateDifficulty()
    {
        currentDifficulty = difficultyCurve.Evaluate(Mathf.Clamp01(totalGameTime / maxDifficultyTime));
        if (difficultyImage != null)
        {
            difficultyImage.color = new Color(difficultyImage.color.r, difficultyImage.color.g, difficultyImage.color.b, currentDifficulty);
        }
    }

    private int failedEventCounter = 0;
    private const int MAX_EVENTS = 3;
    private float failureProbabilityMultiplier = 1f;
    private const float FAILURE_MULTIPLIER_INCREMENT = 0.1f;
    void UpdateEvents()
    {
        if (eventTimer == -1) return;
        currentEventTime += Time.deltaTime * (score != 0 ? Mathf.Lerp(3, .8f, currentDifficulty) : 1f);

        foreach (var evt in activeEvents.ToList())
        {
            if (evt.isActive)
            {
                evt.UpdateEvent();
            }
        }

        if (currentEventTime >= eventTimer - selectedRandomTimeEventDecrease ||
            ShouldForceEventDueToFailures())
        {
            bool extremeMode = PlayerPrefs.GetInt("extremeDifficulty", 0) > 0;

            if (extremeMode || activeEvents.Count < MAX_EVENTS)
            {
                int eventsToStart = CalculateEventsToStart();

                for (int i = 0; i < eventsToStart && activeEvents.Count < MAX_EVENTS; i++)
                {
                    bool eventStarted = StartRandomEvent();

                    if (eventStarted)
                    {
                        failedEventCounter = 0;
                        failureProbabilityMultiplier = 1f;
                    }
                    else
                    {
                        failedEventCounter++;
                        failureProbabilityMultiplier += FAILURE_MULTIPLIER_INCREMENT;
                    }
                }

                currentEventTime = 0;
                selectedRandomTimeEventDecrease = Random.Range(minRandomTimeEventDecrease, maxRandomTimeEventDecrease);
            }
        }
    }

    private bool ShouldForceEventDueToFailures()
    {
        if (failedEventCounter > 0)
        {
            float forcedEventChance = Mathf.Min(0.8f, failedEventCounter * 0.15f * failureProbabilityMultiplier);
            return Random.value < forcedEventChance;
        }
        return false;
    }

    private int CalculateEventsToStart()
    {
        int baseEvents = eventList.Count + 1;

        if (failedEventCounter >= 3)
        {
            baseEvents += Mathf.Min(2, failedEventCounter / 3);
        }

        return Mathf.Min(baseEvents, MAX_EVENTS - activeEvents.Count);
    }

    public void IncreaseChanceOfEvent()
    {
        failureProbabilityMultiplier += FAILURE_MULTIPLIER_INCREMENT;
    }

    public bool ProcessDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        if (ordersManager != null)
        {
            return ordersManager.ProcessOrderDelivery(table, deliveredItem, fromShelf);
        }
        return false;
    }

    public void AddScore(int amount, bool resetTimer = true, bool immediateReset = false)
    {
        this.score += amount;
        if (scoreUI != null)
            scoreUI.text = this.score.ToString();

        if (timer != null)
        {
            if (immediateReset) timer.ResetTimer();
            if (resetTimer)
            {
                setdownItem = true;
                if (ordersManager != null) ordersManager.GenerateNewOrderRequestee();
            }
        }
    }

    public void StartGame()
    {
        gameStarted = true;
        if (ordersManager != null && items.Count > 0)
            ordersManager.GenerateNewOrderRequestee();
    }

    // Event System
    bool StartRandomEvent()
    {
        if (eventList.Count == 0) return false;

        Debug.Log("Event should've started, but I can't fucking deal with this shit. Purge this code if you want to proceed.");
        return false;

        bool extremeMode = PlayerPrefs.GetInt("extremeDifficulty", 0) > 0;

        if (!extremeMode)
        {
            foreach (var evt in activeEvents)
            {
                evt.EndEvent();
                Destroy(evt.gameObject);
            }
            activeEvents.Clear();
        }

        int randomIndex = Random.Range(0, eventList.Count);
        GameObject eventInstance = Instantiate(eventList[randomIndex]);
        Event newEvent = eventInstance.GetComponent<Event>();
        newEvent.StartEvent();
        activeEvents.Add(newEvent);

        StartCoroutine(EndEventAfterDuration(newEvent));
        return true;
    }

    IEnumerator EndEventAfterDuration(Event evt)
    {
        yield return new WaitForSeconds(evt.duration);
        if (activeEvents.Contains(evt))
        {
            evt.EndEvent();
            activeEvents.Remove(evt);
            Destroy(evt.gameObject);
        }
    }

    public Item ReturnItemById(int id)
    {
        foreach (var item in itemTemplates)
        {
            if (item.ID == id)
                return item;
        }
        return null;
    }

    void CheckForGameOver()
    {
        if (timer != null && timer.IsTimeUp())
        {
            GameOver();
        }
    }

    public void ForceGameOver()
    {
        GameOver();
    }

    void GameOver()
    {
        if (!gameStarted) return;
        
        timer.StopTimer();
        
        foreach (var evt in activeEvents)
        {
            evt.EndEvent();
            Destroy(evt.gameObject);
        }
        activeEvents.Clear();

        gameStarted = false;
        var player = FindFirstObjectByType<PlayerController>();
        player.alive = false;

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

        leaderboardEntry = newEntry;
        player.GetComponent<Animator>().Play("GameOver");
        Invoke(nameof(LoadSceneStr), 10f);
    }

    public void LoadSceneOffset(int offset = 0)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + offset);
    }

    public void LoadSceneInd(int ID = 0)
    {
        SceneManager.LoadScene(ID);
    }

    public void LoadSceneStr(string name = "Main Menu")
    {
        SceneManager.LoadScene(name);
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

    public static string GetRandomTauntingName()
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool setdownItem;
    public bool gameStarted;
    [SerializeField] TextMeshProUGUI scoreUI;
    [SerializeField] Image timerUI;
    [SerializeField] TextMeshProUGUI orderListUI;

    [Tooltip("I will kill you if you put something that doesn't have an Item Component here.")]
    [SerializeField] List<GameObject> items = new();
    public List<Item> itemTemplates = new();

    [Tooltip("Spawn box, user must unbox the box. Then they bring wherever they need to.")]
    public GameObject box;
    float timer = 30;
    static readonly float maxTimer = 30;
    int score = 0;

    float progressTimer;

    float currentTime = 0;

    float eventTimer = 60;
    public GameObject talkingDeliveryItem;

    [Header("Order System")]
    [SerializeField] float orderCooldown = 25f;
    [SerializeField] AudioClip[] newOrderSound;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    [Header("Difficulty Settings")]
    [SerializeField] AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] float maxDifficultyTime = 300f; // 5 minutes until max difficulty
    [SerializeField] float minOrderCooldown = 8f; // unused
    [SerializeField] float minOrderTime = 20f;
    [SerializeField] float minEventInterval = 25f; // unused

    private float totalGameTime;
    private float currentDifficulty;


    readonly List<Order> activeOrders = new List<Order>();
    private float orderTimer = 0;
    private AudioSource audioSource;

    [System.Serializable]
    public class Order
    {
        public int requestedItemID;
        public float timeRemaining;
        public float maxTime;
    }

    [SerializeField] List<GameObject> eventList = new List<GameObject>();
    public Event currentEvent;

    public Transform spawnPosition;
    public Transform blackHoleSpawnPosition;

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

        var parent = new GameObject("[Template]s Parent");
        foreach (var item in items)
        {
            var obj=Instantiate(item);
            obj.name = obj.name.Replace("(Clone)", "");
            obj.transform.parent = parent.transform;
            itemTemplates.Add(obj.GetComponent<Item>());
            obj.SetActive(false);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void GenerateNewOrder()
    {
        if (items.Count == 0) return;

        Order newOrder = new Order
        {
            requestedItemID = itemTemplates[Random.Range(0, itemTemplates.Count)].ID,
            timeRemaining = Mathf.Lerp(45f, minOrderTime, currentDifficulty),
            maxTime = Mathf.Lerp(45f, minOrderTime, currentDifficulty)
        };

        activeOrders.Add(newOrder);
        PlaySound(newOrderSound);
        UpdateOrderUI();
    }

    void UpdateOrderUI()
    {
        if (activeOrders.Count < 1)
        {
            orderListUI.text = "Ноль заказов.";
            return;
        }

        orderListUI.text = "";
        foreach (Order order in activeOrders)
        {
            Item item = ReturnItemById(order.requestedItemID);
            float timePercent = order.timeRemaining / order.maxTime;
            string progressBar = GetProgressBar(timePercent);
            orderListUI.text += $"- {item.name} {progressBar} ({Mathf.FloorToInt(order.timeRemaining)}s)\n";
        }
    }

    string GetProgressBar(float percent)
    {
        int bars = 10;
        int filled = Mathf.RoundToInt(bars * percent);
        filled = Mathf.Clamp(filled, 0, bars);
        return "<color=#FF0000>" + new string('█', filled) + "</color>" +
               "<color=#444444>" + new string('█', bars - filled) + "</color>";
    }

    public void ProcessDelivery(Item deliveredItem, bool fromShelf)
    {
        bool orderFound = false;

        foreach (Order order in activeOrders.ToList())
        {
            if (order.requestedItemID == deliveredItem.ID)
            {
                int scoreChange = fromShelf ? deliveredItem.scoreValue * 2 : deliveredItem.scoreValue * -1;
                AddScore(scoreChange, resetTimer: !fromShelf, immediateReset: true);

                activeOrders.Remove(order);
                orderFound = true;
                PlaySound(deliveredItem.fromShelf ? orderCompleteSound : orderFailSound);
                break;
            }
        }

        if (!orderFound)
        {
            AddScore(deliveredItem.scoreValue * (fromShelf ? 0 : -3), resetTimer: !fromShelf);
            PlaySound(orderFailSound);
        }

        UpdateOrderUI();
    }

    void PlaySound(AudioClip[] listOfRandomSounds)
    {
        if (listOfRandomSounds != null && listOfRandomSounds.Length > 0)
        {
            audioSource.PlayOneShot(listOfRandomSounds[Random.Range(0, listOfRandomSounds.Length)]);
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

    public void StartGame()
    {
        gameStarted = true;
        if (items.Count > 0) SpawnItem();
    }

    public void AddScore(int amount, bool resetTimer = true, bool immediateReset = false)
    {
        this.score += amount;
        if (scoreUI != null)
        scoreUI.text = this.score.ToString();
        if (resetTimer)
        {
            setdownItem = true;
            if (immediateReset) ResetTimer();
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
        if (!gameStarted) return;

        totalGameTime += Time.deltaTime;
        currentDifficulty = difficultyCurve.Evaluate(Mathf.Clamp01(totalGameTime / maxDifficultyTime));
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.O))
        {
            StartRandomEvent();
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            timer = maxTimer;
        }
#endif

        timer -= (Time.deltaTime / (currentEvent != null ? 4 : 1 ));  // slow down time if an active event's happening
        currentTime += Time.deltaTime;

        progressTimer = timer / maxTimer;
        timerUI.fillAmount = progressTimer;

        // Bad!
        var color = timerUI.color;
        color.a = progressTimer;
        timerUI.color = color;

        if (timer < 0)
        {
            GameOver();
        }

        if (currentEvent != null)
        {
            if (currentEvent.isActive)
            {
                currentEvent.UpdateEvent();
            }
        }
        else
        {
            if (currentTime >= eventTimer)
            {
                StartRandomEvent();
                currentTime = 0;
            }
        }

        orderTimer += Time.deltaTime;
        if (orderTimer >= orderCooldown)
        {
            GenerateNewOrder();
            orderTimer = 0;
        }

        foreach (Order order in activeOrders.ToList())
        {
            order.timeRemaining -= Time.deltaTime;
            if (order.timeRemaining <= 0)
            {
                AddScore(-25, resetTimer: false);
                activeOrders.Remove(order);
                PlaySound(orderFailSound);
            }
        }

        UpdateOrderUI();
    }

    void StartRandomEvent()
    {
        if (eventList.Count == 0) return;

        if (currentEvent != null)
        {
            currentEvent.EndEvent();
            Destroy(currentEvent.gameObject);
        }

        int randomIndex = Random.Range(0, eventList.Count);
        GameObject eventInstance = Instantiate(eventList[randomIndex]);
        currentEvent = eventInstance.GetComponent<Event>();
        currentEvent.StartEvent();

        StartCoroutine(EndEventAfterDuration(currentEvent));
    }

    public void ResetEventTimer()
    {
        currentTime = 0;
    }

    IEnumerator EndEventAfterDuration(Event evt)
    {
        yield return new WaitForSeconds(evt.duration);
        if (currentEvent == evt)
        {
            evt.EndEvent();
            currentEvent = null;
            Destroy(evt.gameObject);
        }
    }

    public void ForceGameOver()
    {
        GameOver();
    }

    public LeaderboardEntry leaderboardEntry;
    void GameOver()
    {
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
        Invoke(nameof(LoadScene), 10f);
    }

    public void LoadSceneOffset(int offset = 0)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + offset);
    }

    public void LoadScene(int ID = 0)
    {
        SceneManager.LoadScene(ID);
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
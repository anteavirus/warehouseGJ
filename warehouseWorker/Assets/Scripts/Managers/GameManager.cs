using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : GenericManager<GameManager>
{
    [System.Serializable]
    public class GameSave
    {
        public int score = 0;
        public string gamemode = "none";
    }

    public FileDataManipulator gameSaveData;
    public GameSave gameSave = new();

    // Game State
    [SyncVar(hook = nameof(OnGameStartedChanged))]
    public bool gameStarted;
    [SyncVar]
    public bool setdownItem;
    internal int levelSeed = 0;
    [SyncVar(hook = nameof(OnScoreChanged))]
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
    public float minimalDifficulty = 1, maximumDifficulty = 3;
    [SerializeField] float maxDifficultyTime = 120f;
    private float totalGameTime;
    // EDITED: Made currentDifficulty public so timers can access it
    public float currentDifficulty;

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

    public override void Initialize()
    {
        base.Initialize();
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Dupe game manager; killed.");  // i think the base stuff doesn't work at all. it's. :(
            Destroy(gameObject);
            return;
        }
        levelSeed = UnityEngine.Random.Range(0, 1969);
        InitializeItemTemplates();
        InitializeAudio();
        InitializeManagers();        
        
        // timer is only initialized after other shit...
        gameSaveData = FileDataManipulator.ForPersistentDataPath(gameSave, new string[] { "save.sav" });
        try
        {
            if (gameSaveData.FileExists())
            {
            // load data from save too
            // TODO: perhaps make it async? it can force the game to bend over in these moments...
                var temp = gameSaveData.LoadData<GameSave>();
                if (temp.gamemode == timer.gamemode)  // same gamemode, or rather timer lets be honest.
                {
                    gameSave = temp;
                    score = gameSave.score;
                }
            }
        }
        catch
        {
            Debug.LogWarning("Loading savedata failed. I wish I cared enough to not shove it into a trycatch statement but I've got bigger fishes to fry");
        }

    }

    void InitializeItemTemplates()
    {
        var existing = transform.Find("[Template]s Parent");
        if (!existing.IsTrulyNull())
        {
            Destroy(existing.gameObject);
        }
        var parent = new GameObject("[Template]s Parent");
        foreach (var item in items)
        {
            var obj = Instantiate(item);
            obj.transform.SetParent(transform);
            var localname = obj.GetComponent<LocalizedText>();
            localname.UpdateText();
            obj.name = localname.text;
            obj.transform.parent = parent.transform;
            var itemComp = obj.GetComponent<Item>();
            itemComp.mixerGroup = sfx;
            
            // Ensure NetworkIdentity exists on templates (but don't spawn them - they're templates)
            if (obj.GetComponent<NetworkIdentity>() == null)
            {
                obj.AddComponent<NetworkIdentity>();
            }
            
            itemTemplates.Add(itemComp);
            obj.SetActive(false);
        }
        parent.transform.SetParent(transform);
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
        var player = FindObjectOfType<PlayerController>(true)?.GetComponent<SerializableDictionaryObjectContainer>();
        if (player == null) return; // we probably dont have to do this right now? something sometihng me complaining
        if (scoreUI == null)
        {
            scoreUI = player.Fetch("scoreUI").GetComponent<TextMeshProUGUI>();
        }

        if (timerUI == null)
        {
            timerUI = player.Fetch("timerCircle").GetComponent<Image>();
        }

        if (difficultyImage == null)
        {
            difficultyImage = player.Fetch("timerFire").GetComponent<Image>();
        }

        if ( blackHoleSpawnPosition == null)
        {
            blackHoleSpawnPosition = GameObject.Find("black hole spawn")?.transform;
        }

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

    // EDITED: Update now handles server/client properly
    void Update()
    {
        if (!gameStarted) return;

        UpdateGameTime();
        UpdateDifficulty();
        
        // EDITED: Events and timer updates only on server
        if (isServer)
        {
            UpdateEvents();
            if (timer != null && timer.enabledTimer) timer.UpdateTimer();
        }
        
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
                        if (Time.timeScale != 0)
                        {
                            failedEventCounter++;
                            failureProbabilityMultiplier += FAILURE_MULTIPLIER_INCREMENT;
                        }
                    }
                }

                currentEventTime = 0;
                selectedRandomTimeEventDecrease = UnityEngine.Random.Range(minRandomTimeEventDecrease, maxRandomTimeEventDecrease);
            }
        }
    }

    private bool ShouldForceEventDueToFailures()
    {
        if (failedEventCounter > 0)
        {
            float forcedEventChance = Mathf.Min(0.8f, failedEventCounter * 0.15f * failureProbabilityMultiplier);
            return UnityEngine.Random.value < forcedEventChance;
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

    // EDITED: Fixed score synchronization - now properly handles server/client calls
    public void AddScore(int amount, bool resetTimer = true, bool immediateReset = false)
    {
        if (isServer)
        {
            // Server directly updates score (SyncVar will sync to clients)
            score += amount;
            
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
        else
        {
            // Client requests score change from server
            CmdAddScore(amount, resetTimer, immediateReset);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdAddScore(int amount, bool resetTimer, bool immediateReset)
    {
        // Server validates and updates score
        score += amount;

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

    private void OnScoreChanged(int oldScore, int newScore)
    {
        // EDITED: Score hook now updates UI on all clients when score changes
        if (scoreUI != null)
            scoreUI.text = newScore.ToString();
    }

    private void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        gameStarted = newValue;
    }

    [Server]
    public void StartGame()
    {
        gameSave.score = score;  // TODO: save shit to save somewhere else probably
        try
        {
            gameSaveData.SaveData(gameSave);

        }
        catch
        {
            Debug.LogWarning("oh no. gameSaveData failed to save data game save. oh no. i am putting this into a trycatch statement. oh no im not giving a fuck............");
        }

        gameStarted = true;
        if (ordersManager != null && items.Count > 0)
            ordersManager.GenerateNewOrderRequestee();
        
        // Start timer on all clients
        RpcStartGame();
    }
    
    [ClientRpc]
    private void RpcStartGame()
    {
        if (timer != null)
        {
            timer.StartTimer();
        }
    }

    // Event System
    [Server]
    bool StartRandomEvent()
    {
        if (eventList.Count == 0) return false;

        // EDITED: Re-enabled event system - events now properly spawn and sync across network
        bool extremeMode = PlayerPrefs.GetInt("extremeDifficulty", 0) > 0;

        if (!extremeMode)
        {
            foreach (var evt in activeEvents)
            {
                evt.EndEvent();
                NetworkServer.Destroy(evt.gameObject);
            }
            activeEvents.Clear();
        }

        int randomIndex = UnityEngine.Random.Range(0, eventList.Count);
        GameObject eventInstance = Instantiate(eventList[randomIndex]);
        
        // Ensure NetworkIdentity exists for events
        if (eventInstance.GetComponent<NetworkIdentity>() == null)
        {
            eventInstance.AddComponent<NetworkIdentity>();
        }
        
        // Spawn event on network
        NetworkServer.Spawn(eventInstance);
        
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
            NetworkServer.Destroy(evt.gameObject);
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

    // EDITED: CheckForGameOver now only runs on server
    void CheckForGameOver()
    {
        if (!isServer) return;
        
        if (timer != null && timer.IsTimeUp())
        {
            GameOver();
        }
    }

    // EDITED: ForceGameOver now properly calls server method
    public void ForceGameOver()
    {
        if (isServer)
        {
            GameOver();
        }
        else
        {
            CmdForceGameOver();
        }
    }
    
    [Command(requiresAuthority = false)]
    private void CmdForceGameOver()
    {
        GameOver();
    }

    // EDITED: Game over now properly handles multiplayer - all players see game over
    [Server]
    void GameOver()
    {
        if (!gameStarted) return;

        try
        {
            
        gameSaveData.DeleteFile();  // Oops, you died, autosave fucked over xd // TODO: is this fine? figure shit out.
        }
        catch
        {
            // Piss off  cunt
        }
        timer.StopTimer();
        
        foreach (var evt in activeEvents)
        {
            evt.EndEvent();
            NetworkServer.Destroy(evt.gameObject);
        }
        activeEvents.Clear();

        gameStarted = false;
        
        // EDITED: Set all players to dead, not just one
        RpcGameOver();
    }
    
    [ClientRpc]
    private void RpcGameOver()
    {
        // Set all players to dead
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            player.alive = false;
        }
        
        // Only handle leaderboard on server (or local player)
        if (isServer)
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
            leaderboardEntry = newEntry;
        }
        
        // Play game over animation on local player
        var localPlayer = FindFirstObjectByType<PlayerController>();
        if (localPlayer != null && localPlayer.isLocalPlayer)
        {
            var animator = localPlayer.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("GameOver");
            }
        }
        
        // Load scene after delay (only on server)
        if (isServer)
        {
            Invoke(nameof(LoadSceneStr), 10f);
        }
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


    // TODO: fucking shove this somewhere else, some other seperate leaderboard class which'll at least visually debloat this slightly
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

        return tauntingNames[UnityEngine.Random.Range(0, tauntingNames.Length)];
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
        return validTemplates[UnityEngine.Random.Range(0, validTemplates.Count)];
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
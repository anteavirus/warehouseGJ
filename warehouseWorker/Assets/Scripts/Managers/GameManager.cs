using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pure game-logic manager. No SyncVars, no Commands, no RPCs.
/// All network state lives in <see cref="GameNetworkBridge"/>.
/// </summary>
public class GameManager : GenericManager<GameManager>
{
    #region Save data

    [System.Serializable]
    public class GameSave
    {
        public int score = 0;
        public string gamemode = "none";
    }

    public FileDataManipulator gameSaveData;
    public GameSave gameSave = new();

    #endregion

    #region Manager references

    [Header("Manager References")]
    public ShelvesStockManager shelvesStockManager;
    public OrdersManager ordersManager;
    public GenericTimer timer;
    private AudioSource audioSource;

    #endregion

    #region UI

    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI scoreUI;
    [SerializeField] public Image timerUI;
    [SerializeField] Image difficultyImage;

    #endregion

    #region Items & templates

    [Header("Items")]
    [Tooltip("I will kill you if you put something that doesn't have an Item Component here.")]
    public List<GameObject> items = new();

    #endregion

    #region Spawning

    [Header("Spawning")]
    public Transform blackHoleSpawnPosition;

    #endregion

    #region Audio

    [Header("Audio")]
    public AudioMixerGroup sfx;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    #endregion

    #region Difficulty

    [Header("Difficulty Settings")]
    [SerializeField] AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float minimalDifficulty = 1, maximumDifficulty = 3;
    [SerializeField] float maxDifficultyTime = 120f;
    private float totalGameTime;
    public float currentDifficulty;

    #endregion

    #region Events

    [Header("Events System")]
    [SerializeField] List<GameObject> eventList = new List<GameObject>();
    public List<Event> activeEvents = new List<Event>();
    [SerializeField] float minRandomTimeEventDecrease = 1, maxRandomTimeEventDecrease = 15;
    private float currentEventTime = 0;
    [SerializeField] private float eventTimer = 60;
    private float selectedRandomTimeEventDecrease = 0;

    #endregion

    #region Leaderboard

    [Header("Leaderboard")]
    public LeaderboardEntry leaderboardEntry;

    #endregion

    #region Proxy properties (convenience — keeps old call-sites compiling)

    /// <summary>True when the bridge exists and its gameStarted SyncVar is set.</summary>
    public bool GameStarted => Bridge?.gameStarted ?? false;

    /// <summary>Current score, read from the bridge.</summary>
    public int Score => Bridge?.score ?? 0;

    /// <summary>Shortcut so old code that checked isServer on this manager still works.</summary>
    public bool IsServer => Bridge != null && Bridge.isServer;

    /// <summary>Internal reference to the network bridge.</summary>
    public GameNetworkBridge Bridge { get; private set; }

    #endregion

    #region Initialize

    public override void Initialize()
    {
        base.Initialize();
        if (Instance == null || Instance == this)
            Instance = this;
        else
        {
            Debug.LogWarning("Duplicate GameManager destroyed.");
            Destroy(gameObject);
            return;
        }

        // Grab the bridge — it should already be spawned by NetworkGameManager.
        Bridge = GameNetworkBridge.Instance;

        // Server sets level seed so shelf layout is consistent across clients.
        if (IsServer)
        {
            int seed = UnityEngine.Random.Range(0, 1969);
            Bridge.levelSeed = seed;
        }

        InitializeAudio();

        var player = FindObjectOfType<PlayerController>(true)?.GetComponent<SerializableDictionaryObjectContainer>();
        if (player == null) return;

        /// hack
        if (scoreUI == null)
            scoreUI = ((GameObject) player.Fetch("scoreUI"))?.GetComponent<TextMeshProUGUI>();
        if (timerUI == null)
            timerUI = ((GameObject)player.Fetch("timerCircle"))?.GetComponent<Image>();
        if (difficultyImage == null)
            difficultyImage = ((GameObject)player.Fetch("timerFire"))?.GetComponent<Image>();
        if (blackHoleSpawnPosition == null)
            blackHoleSpawnPosition = GameObject.Find("black hole spawn")?.transform;

        InitializeManagers();

        gameSaveData = FileDataManipulator.ForPersistentDataPath(gameSave, new string[] { "save.sav" });
        try
        {
            if (gameSaveData.FileExists())
            {
                var temp = gameSaveData.LoadData<GameSave>();
                if (temp.gamemode == timer?.gamemode)
                {
                    gameSave = temp;
                    if (Bridge != null)
                        Bridge.score = gameSave.score;
                }
            }
        }
        catch
        {
            Debug.LogWarning("Loading savedata failed — swallowing exception as usual.");
        }

        // Subscribe to bridge events so UI stays in sync.
        if (Bridge != null)
        {
            Bridge.ScoreChanged += OnScoreChangedFromBridge;
            Bridge.GameStartedChanged += OnGameStartedChangedFromBridge;
        }
    }

    #endregion

    #region Audio

    void InitializeAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    #endregion

    #region Manager bootstrapping

    void InitializeManagers()
    {
        if (IsServer)
        {
            if (shelvesStockManager != null)
            {
                shelvesStockManager.Initialize(this);
                shelvesStockManager.Work();
            }

            if (ordersManager != null)
                ordersManager.Initialize(this);

            if (timer != null)
                timer.Initialize(this);
        }
    }

    #endregion

    #region Update loop

    void Update()
    {
        if (!GameStarted) return;

        UpdateGameTime();
        UpdateDifficulty();

        if (IsServer)
        {
            UpdateEvents();
            if (timer != null && timer.enabledTimer)
                timer.UpdateTimer();
        }

        if (ordersManager != null)
            ordersManager.UpdateOrders();

        CheckForGameOver();
    }

    void UpdateGameTime() => totalGameTime += Time.deltaTime;

    void UpdateDifficulty()
    {
        currentDifficulty = difficultyCurve.Evaluate(Mathf.Clamp01(totalGameTime / maxDifficultyTime));
        if (difficultyImage != null)
        {
            difficultyImage.color = new Color(
                difficultyImage.color.r,
                difficultyImage.color.g,
                difficultyImage.color.b,
                currentDifficulty);
        }
    }

    #endregion

    #region Events system

    private int failedEventCounter = 0;
    private const int MAX_EVENTS = 3;
    private float failureProbabilityMultiplier = 1f;
    private const float FAILURE_MULTIPLIER_INCREMENT = 0.1f;

    void UpdateEvents()
    {
        if (!IsServer) return;
        if (eventTimer == -1) return;

        int currentScore = Bridge?.score ?? 0;
        currentEventTime += Time.deltaTime * (currentScore != 0 ? Mathf.Lerp(3, .8f, currentDifficulty) : 1f);

        foreach (var evt in activeEvents.ToList())
        {
            if (evt.isActive)
                evt.UpdateEvent();
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
            float chance = Mathf.Min(0.8f, failedEventCounter * 0.15f * failureProbabilityMultiplier);
            return UnityEngine.Random.value < chance;
        }
        return false;
    }

    private int CalculateEventsToStart()
    {
        int baseEvents = eventList.Count + 1;
        if (failedEventCounter >= 3)
            baseEvents += Mathf.Min(2, failedEventCounter / 3);
        return Mathf.Min(baseEvents, MAX_EVENTS - activeEvents.Count);
    }

    public void IncreaseChanceOfEvent() => failureProbabilityMultiplier += FAILURE_MULTIPLIER_INCREMENT;

    bool StartRandomEvent()
    {
        if (eventList.Count == 0) return false;

        bool extremeMode = PlayerPrefs.GetInt("extremeDifficulty", 0) > 0;
        if (!extremeMode)
        {
            foreach (var evt in activeEvents)
            {
                evt.EndEvent();
                if (Bridge != null) Bridge.ServerDestroy(evt.gameObject);
            }
            activeEvents.Clear();
        }

        int idx = UnityEngine.Random.Range(0, eventList.Count);
        GameObject eventInstance = Instantiate(eventList[idx]);

        if (eventInstance.GetComponent<NetworkIdentity>() == null)
            eventInstance.AddComponent<NetworkIdentity>();

        if (Bridge != null) Bridge.ServerSpawn(eventInstance);

        Event newEvent = eventInstance.GetComponent<Event>();
        newEvent.StartEvent();
        newEvent.RpcStartEvent();
        activeEvents.Add(newEvent);

        StartCoroutine(EndEventAfterDuration(newEvent));
        return true;
    }

    IEnumerator EndEventAfterDuration(Event evt)
    {
        yield return new WaitForSeconds(evt.duration);
        if (activeEvents.Contains(evt))
        {
            evt.RpcEndEvent();
            evt.EndEvent();
            activeEvents.Remove(evt);
            Bridge?.ServerDestroy(evt.gameObject);
        }
    }

    /// <summary>Called by GameNetworkBridge during server-side game over to tear down events.</summary>
    public void ClearActiveEvents()
    {
        foreach (var evt in activeEvents)
        {
            evt.RpcEndEvent();
            evt.EndEvent();
            if (Bridge != null) Bridge.ServerDestroy(evt.gameObject);
        }
        activeEvents.Clear();
    }

    #endregion

    #region Score

    /// <summary>Called from anywhere — delegates to the bridge which handles server/client routing.</summary>
    public void AddScore(int amount, bool resetTimer = true, bool immediateReset = false)
    {
        Bridge?.RequestAddScore(amount, resetTimer, immediateReset);
    }

    public bool ProcessDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        return ordersManager != null && ordersManager.ProcessOrderDelivery(table, deliveredItem, fromShelf);
    }

    #endregion

    #region Bridge event handlers

    private void OnScoreChangedFromBridge(int newScore)
    {
        if (scoreUI != null)
            scoreUI.text = newScore.ToString();
    }

    private void OnGameStartedChangedFromBridge(bool newValue)
    {
        Debug.Log($"[GameManager] gameStarted changed to {newValue}");
    }

    #endregion

    #region Game flow — delegates to bridge

    /// <summary>Kicks off the game. Server-only in practice.</summary>
    public void StartGame()
    {
        Bridge?.ServerStartGame();
    }

    public void PauseGame()
    {
        Bridge?.ServerPauseGame();
    }

    void CheckForGameOver()
    {
        if (!IsServer) return;
        if (timer != null && timer.IsTimeUp())
            ForceGameOver();
    }

    public void ForceGameOver()
    {
        Bridge?.RequestGameOver();
    }

    #endregion

    #region Items

    public Item ReturnItemById(int id)
    {
        foreach (var item in items)
        {
            if (item.TryGetComponent<Item>(out var itemComp) && itemComp.ID == id)
                return itemComp;
        }
        return null;
    }

    #endregion

    #region Leaderboard

    /// <summary>Called by GameNetworkBridge.RpcGameOver on the server side.</summary>
    public void HandleLeaderboardOnServer()
    {
        if (!IsServer) return;

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
            leaderboard.entries = leaderboard.entries.GetRange(0, 10);

        SaveLeaderboard(leaderboard);
        leaderboardEntry = newEntry;
    }

    LeaderboardEntry CreateLeaderboardEntry()
    {
        string username = PlayerPrefs.GetString("CurrentUsername", "");
        if (string.IsNullOrEmpty(username))
            username = GetRandomTauntingName();

        return new LeaderboardEntry { name = username, score = Bridge?.score ?? 0 };
    }

    public static string GetRandomTauntingName()
    {
        string[] names = {
            "OofEnthusiast", "SweatySocks", "Bunnyhopper", "FumbleChamp", "ConfettiCannon",
            "PotatoAim", "ProSK8R", "LootGnoblin", "CertifiedDerp", "ParticipationPrize"
        };
        return names[UnityEngine.Random.Range(0, names.Length)];
    }

    LeaderboardWrapper LoadLeaderboard()
    {
        string json = PlayerPrefs.GetString("Leaderboard", "");
        if (!string.IsNullOrEmpty(json))
            return JsonUtility.FromJson<LeaderboardWrapper>(json);
        return new LeaderboardWrapper();
    }

    static void SaveLeaderboard(LeaderboardWrapper lb)
    {
        PlayerPrefs.SetString("Leaderboard", JsonUtility.ToJson(lb));
        PlayerPrefs.Save();
    }

    public static void CreateLeaderBoard()
    {
        if (PlayerPrefs.HasKey("Leaderboard")) return;

        LeaderboardWrapper wrapper = LoadRandomLeaderboardTemplate();
        if (wrapper == null || wrapper.entries.Count == 0)
            CreateDefaultLeaderboard(ref wrapper);

        wrapper.entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (wrapper.entries.Count > 10)
            wrapper.entries = wrapper.entries.GetRange(0, 10);

        SaveLeaderboard(wrapper);
    }

    static LeaderboardWrapper LoadRandomLeaderboardTemplate()
    {
        TextAsset[] files = Resources.LoadAll<TextAsset>("Leaderboards");
        if (files == null || files.Length == 0) return null;

        var valid = new List<LeaderboardWrapper>();
        foreach (TextAsset f in files)
        {
            try
            {
                var t = JsonUtility.FromJson<LeaderboardWrapper>(f.text);
                if (t != null && t.entries.Count > 0) valid.Add(t);
            }
            catch { Debug.LogWarning($"Failed to parse leaderboard template: {f.name}"); }
        }
        return valid.Count == 0 ? null : valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    static void CreateDefaultLeaderboard(ref LeaderboardWrapper w)
    {
        w = new LeaderboardWrapper();
        w.entries.AddRange(new List<LeaderboardEntry> {
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

    #endregion

    #region Scene loading

    public void LoadSceneOffset(int offset = 0)
        => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + offset);

    public void LoadSceneInd(int ID = 0)
        => SceneManager.LoadScene(ID);

    public void LoadSceneStr(string name = "Main Menu")
        => SceneManager.LoadScene(name);

    #endregion
}

#region Leaderboard serialisable types

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

#endregion

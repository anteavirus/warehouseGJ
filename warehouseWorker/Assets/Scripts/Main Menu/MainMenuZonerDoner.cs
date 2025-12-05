using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class MainMenuZonerDoner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject boxPrefab;
    public GameObject[] itemPrefabs;
    public GameObject extraSpecialPunchCardPrefab;
    public Transform spawnPoint;
    public Transform extraSpecialPunchCardSpawnPoint;
    public float spawnInterval = 2f;
    public float minIntervalRandom = 2f;
    public float maxIntervalRandom = 6f;
    public Vector2 spawnForceRange = new Vector2(1f, 3f);

    private float spawnTimer;
    private List<GameObject> activeItems = new List<GameObject>();

    [Header("UI")]
    [SerializeField] GameObject leaderboardObj;
    [SerializeField] TextMeshProUGUI leaderboardText;

    [Header("Player Positioning")]
    public MainMenuPlayerController playerController;
    public Transform playerDefaultPosition;
    public Transform playerSpecialPosition;
    private GameObject currentExtraCard;

    // TODO:  fuckign ugly kys
    public GameObject endlessGamemodeTimerPrefab;
    public GameObject shiftsGamemodeTimerPrefab;

    public AudioMixerGroup sfx;

    private void Start()
    {
        Time.timeScale = 1;
        spawnTimer = spawnInterval;
        GameManager.CreateLeaderBoard();
        LoadLeaderboard();
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnItem();
            spawnTimer = spawnInterval + Random.Range(minIntervalRandom, maxIntervalRandom);
        }
    }

    void SpawnItem()
    {
        if (itemPrefabs.Length == 0) return;

        GameObject newObject;
        if (boxPrefab != null)
        {
            GameObject newBox = Instantiate(boxPrefab);
            newBox.GetComponent<Box>().containedItem = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
            newObject = newBox;
        }
        else
        {
            newObject = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
        }

        GameObject newItem = Instantiate(
            newObject,
            spawnPoint.position,
            Random.rotation
        );

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 randomForce = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1.5f),
                Random.Range(-1f, 1f)
            ).normalized * Random.Range(spawnForceRange.x, spawnForceRange.y);

            rb.AddForce(randomForce, ForceMode.Impulse);
        }

        Item itemComponent = newItem.AddComponent<Item>();
        itemComponent.mixerGroup = sfx;
        activeItems.Add(newItem);
    }

    public void RemoveItem(GameObject item)
    {
        if (activeItems.Contains(item))
        {
            activeItems.Remove(item);
            Destroy(item);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (activeItems.Contains(other.gameObject))
        {
            RemoveItem(other.gameObject);
        }
    }

    void LoadLeaderboard()
    {
        var leaderboard = PlayerPrefs.GetString("Leaderboard");
        var list = JsonUtility.FromJson<LeaderboardWrapper>(leaderboard);
        int i = 0;
        string str = "";
        foreach (var item in list.entries)
        {
            i++;
            string unWrappableInt = $"<nobr>{item.score}</nobr>";
            str += $"{i}: {item.name} - {unWrappableInt}\n";
        }
        try
        {

        leaderboardText.text = str;
        }
        catch { /*kick balls*/}
    }

    public void LoadScene(int id)
    {
        SceneManager.LoadScene(id);
    }

    public void LoadSceneStr(string name)
    {
        SceneManager.LoadScene(name);
    }

    // TODO: when actually making multiplayer, we'll need to make ANOTHER screen for either CONNECTING to the lobby, or CREATING such lobby. at least make a stupid ass p2p connection; in theory we wish to make a source-like connection possible where the game also has a list of ips it can try to show the player if it's available including player own's, but so far? i have no hope 
    // TODO: another todo, great; honestly, there SHOULDNT EVER BE MULTIPLE SCENES that are BASICALLY EXACTLY THE SAME. Surely we can just use a single scene, and then - on the fly - throw in the appropriate scripts. Like, case 0: Okay, load the scene async (SET TIME DILATION TO 0 or whatever the name is, can't have shit happen!), and on finishing, add appropriate classes. Like, "GenericTimer" if it's something. that's. uh. something. piss on player
    public void BeginPlayerTransportationToGameplay()
    {
        GameObject timerMan = MasterManager.Instance.transform.Find("TimerManager").gameObject;
        GameObject tempShitFuckDev;
        playerController.MoveToPosition(playerSpecialPosition, () => {
            string coderIsFucker;
            // Not good, not great, not horrible; let's hope this hack won't get pushed to production
            switch (PlayerPrefs.GetInt("gamemodeSelected", 0))
            {
                case 0:
                    {
                        coderIsFucker = "GameplayScene";
                        // master manager, give me timerManager; then create the prefab, attach the component from the prefab (shifts/endless/etc) to the timerManager (destroy one if already exists), and destroy the prefab
                        tempShitFuckDev = Instantiate(endlessGamemodeTimerPrefab);
                        foreach (var item in UsefulStuffs.FindComponentsInChildren<Transform>(timerMan))
                        {
                            if (item == timerMan.transform) continue;  // not u gtfo
                            Destroy(item.gameObject);   // DESTROY
                        }
                        tempShitFuckDev.transform.SetParent(timerMan.transform);
                        GameManager.Instance.timer = tempShitFuckDev.GetComponent<EndlessGamemodeTimer>();
                        Debug.Log(tempShitFuckDev.GetComponent<GenericTimer>());
                        if (GameManager.Instance.timer == null)
                            GameManager.Instance.timer = tempShitFuckDev.GetComponent<GenericTimer>();
                        // FUCKKKKKKKKK. IT NEEDS TO BE FULLY COPIED :(
                        // FUCk you I'm going all hack
                    }
                    break;
                case 1:
                    {
                        coderIsFucker = "GameplayShiftsScene";
                        tempShitFuckDev = Instantiate(shiftsGamemodeTimerPrefab);
                        foreach (var item in UsefulStuffs.FindComponentsInChildren<Transform>(timerMan))
                        {
                            if (item == timerMan.transform) continue;  // not u gtfo
                            Destroy(item.gameObject);   // DESTROY
                        }
                        tempShitFuckDev.transform.SetParent(timerMan.transform);
                        GameManager.Instance.timer = tempShitFuckDev.GetComponent<ShiftsGamemodeTimer>();
                        Debug.Log(tempShitFuckDev.GetComponent<GenericTimer>());
                        if (GameManager.Instance.timer == null)
                            GameManager.Instance.timer = tempShitFuckDev.GetComponent<GenericTimer>();
                    }
                    break;
                case 2:
                    {
                        coderIsFucker = "afuckingscenethatdoesn'texistbecausewedon'thavesuchafuckingstupidgamemodetosufferthroughyet";
                        coderIsFucker = "Though we're still going to load *SOME* thing because *OF COURSE* someone will WANT TO PRESS THIS FUCKIN BUTTON. BECAUSE OF COURSE THIS SHIT WILL GO PUBLIC SOMEHOW";
                        coderIsFucker = "GameplayScene";
                        tempShitFuckDev = Instantiate(endlessGamemodeTimerPrefab);
                        foreach (var item in UsefulStuffs.FindComponentsInChildren<Transform>(timerMan))
                        {
                            if (item == timerMan) continue; 
                            Destroy(item.gameObject);  
                        }
                        tempShitFuckDev.transform.SetParent(timerMan.transform);
                        GameManager.Instance.timer = tempShitFuckDev.GetComponent<GenericTimer>();
                        Debug.Log(tempShitFuckDev.GetComponent<GenericTimer>());
                        if (GameManager.Instance.timer == null)
                            GameManager.Instance.timer = tempShitFuckDev.GetComponent<GenericTimer>();
                    }
                    break;
                default:
                    {
                        Debug.LogError("Transportation impossible: Govno za komputerom (net, ne vy (esly ty ne koder moi))");
                        return;
                    }

            }


            SceneManager.LoadScene(coderIsFucker);
        });
    }
}

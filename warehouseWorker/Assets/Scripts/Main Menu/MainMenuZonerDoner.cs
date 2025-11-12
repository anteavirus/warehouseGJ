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
    public void BeginPlayerTransportationToGameplay()
    {
        playerController.MoveToPosition(playerSpecialPosition, () => {
            string coderIsFucker;
            switch (PlayerPrefs.GetInt("gamemodeSelected", 0))
            {
                case 0:
                    {
                        coderIsFucker = "GameplayScene";
                    }
                    break;
                case 1:
                    {
                        coderIsFucker = "GameplayShiftsScene";
                    }
                    break;
                case 2:
                    {
                        coderIsFucker = "afuckingscenethatdoesn'texistbecausewedon'thavesuchafuckingstupidgamemodetosufferthroughyet";
                    }
                    break;
                default:
                    {
                        Debug.LogError("Transportation impossible: Govno za komputerom");
                        return;
                    }

            }
            SceneManager.LoadScene(coderIsFucker);
        });
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuZonerDoner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject[] itemPrefabs;
    public GameObject extraSpecialPunchCardPrefab;
    public Transform spawnPoint;
    public Transform extraSpecialPunchCardSpawnPoint;
    public float spawnInterval = 2f;
    public float minIntervalRandom = 2f;
    public float maxIntervalRandom = 6f;
    public Vector2 spawnForceRange = new Vector2(1f, 3f);

    [Header("Zone Settings")]
    public Collider spawnZone;

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

        GameObject newItem = Instantiate(
            itemPrefabs[Random.Range(0, itemPrefabs.Length)],
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
            string unWrappableInt = item.score < 0 ? $"<nobr>{item.score}</nobr>" : item.score.ToString();
            str += $"{i}: {item.name} - {unWrappableInt}\n";
        }
        leaderboardText.text = str;
    }

    public void LoadScene(int id)
    {
        SceneManager.LoadScene(id);
    }

    public void UpdateGameSettings(float volume, int qualityLevel)
    {
        // Example settings that can be changed mid-game
        AudioListener.volume = volume;
        QualitySettings.SetQualityLevel(qualityLevel);
    }

    public void SpawnExtraSpecialPunchCard()
    {
        if (currentExtraCard != null)
        {
            if (currentExtraCard.TryGetComponent<MainMenuExtraSpecialPunchCard>(out var oldCard)) 
                oldCard.OnDestroyed -= HandleCardDestroyed;
            Destroy(currentExtraCard);
        }

        StartCoroutine(SpawnAfterPlayerMovement());
    }

    private IEnumerator SpawnAfterPlayerMovement()
    {
        playerController.MoveToPosition(playerSpecialPosition, () => {
            Vector3 raycastStart = extraSpecialPunchCardSpawnPoint.position;
            Vector3 spawnPos = extraSpecialPunchCardSpawnPoint.position;

            if (Physics.Raycast(raycastStart, Vector3.down, out RaycastHit hit, 20f))
            {
                spawnPos = hit.point + Vector3.up * 0.1f;
            }

            currentExtraCard = Instantiate(
                extraSpecialPunchCardPrefab,
                spawnPos,
                Quaternion.Euler(0f, 0f, 0f)
            );

            var newCard = currentExtraCard.GetComponent<MainMenuExtraSpecialPunchCard>();
            newCard.OnDestroyed += HandleCardDestroyed;
            newCard.InitializeHoverPosition(spawnPos);
        });

        yield return null;
    }

    private void HandleCardDestroyed()
    {
        currentExtraCard = null;
        try
        {
            playerController.MoveToPosition(playerDefaultPosition);
        }
        catch
        {
            Debug.LogWarning("Coroutine sacked, memory leaked, code spaghettied. Everything's fine.");
        }
    }
}

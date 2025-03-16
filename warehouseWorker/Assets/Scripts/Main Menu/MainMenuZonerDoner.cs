using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainMenuZonerDoner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject[] itemPrefabs;
    public Transform spawnPoint;
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
    public UIManager uiManager;

    private void Start()
    {
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
            string unWrappableInt = item.score < 0 ? $"<nobr>{item.score.ToString()}</nobr>" : item.score.ToString();
            str += $"{i}: {item.name} - {unWrappableInt}\n";
        }
        leaderboardText.text = str;
    }

    public void UpdateGameSettings(float volume, int qualityLevel)
    {
        // Example settings that can be changed mid-game
        AudioListener.volume = volume;
        QualitySettings.SetQualityLevel(qualityLevel);
    }
}

using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// Shelf spawning & stock management. Pure MonoBehaviour — delegates
/// network spawning to <see cref="GameNetworkBridge"/>.
/// </summary>
public class ShelvesStockManager : GenericManager<ShelvesStockManager>
{
    public GameManager gameManager;
    public List<GameObject> shelfPrefabs;
    public List<ShelfSpawn> shelfSpawns;

    [Header("Stock Settings")]
    public int minInitialStock = 3;
    public int maxInitialStock = 8;

    [Header("Debug")]
    [SerializeField] private int assignmentSeed = 0;

    [Tooltip("Hi I am storageAreas from shelfPrefabs")]
    public List<StorageArea> shelfStorages;

    private Dictionary<StorageArea, GameObject> storageAreaToPrefabMap = new();

    private bool IsServer => GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isServer;

    #region Initialize

    public override void Initialize()
    {
        base.Initialize();
        if (gameManager == null)
            gameManager = GameManager.Instance;
        if (shelfSpawns.All(x => x == null))
            shelfSpawns = FindObjectsOfType<ShelfSpawn>().ToList();
    }

    public void Initialize(GameManager gm)
    {
        base.Initialize();
        gameManager = gm;
        if (shelfSpawns.All(x => x == null))
            shelfSpawns = FindObjectsOfType<ShelfSpawn>().ToList();
    }

    #endregion

    #region Storage area mapping

    public void UpdateShelfStoragesFromPrefabs()
    {
        shelfStorages.Clear();
        storageAreaToPrefabMap.Clear();

        if (shelfPrefabs?.Count <= 0) return;

        foreach (var prefab in shelfPrefabs)
        {
            if (prefab == null) continue;
            foreach (var area in prefab.GetComponentsInChildren<StorageArea>(true))
            {
                if (!shelfStorages.Contains(area))
                {
                    shelfStorages.Add(area);
                    storageAreaToPrefabMap[area] = prefab;
                }
            }
        }
    }

    #endregion

    #region Work — server-side shelf placement

    public void Work()
    {
        assignmentSeed = gameManager != null
            ? GameNetworkBridge.Instance?.levelSeed ?? (int)System.DateTime.Now.Ticks
            : (int)System.DateTime.Now.Ticks;
        Random.InitState(assignmentSeed);

        Debug.Log($"[ShelfManager] Using seed: {assignmentSeed}");

        UpdateShelfStoragesFromPrefabs();

        var availableItems = gameManager?.items
            .Select(i => i.GetComponent<Item>())
            .Where(c => c != null)
            .ToList() ?? new List<Item>();

        var activeSpawns = shelfSpawns.Where(sp => sp != null && sp.IsActiveForAssignment()).ToList();
        SpawnShelvesAtSpawnPoints(activeSpawns);
    }

    private void SpawnShelvesAtSpawnPoints(List<ShelfSpawn> spawnPoints)
    {
        if (!IsServer) return;

        foreach (var spawnPoint in spawnPoints)
        {
            if (spawnPoint.IsAssigned())
            {
                SpawnShelfForSpawn(spawnPoint);
            }
            else
            {
                SpawnRandomEmptyShelf(spawnPoint);
            }
        }
    }

    private void SpawnShelfForSpawn(ShelfSpawn spawnPoint)
    {
        var prefabToSpawn = GetRootPrefabForStorageArea(spawnPoint.assignedShelfPrefab);
        if (prefabToSpawn == null)
        {
            Debug.LogError($"Could not find root prefab for StorageArea {spawnPoint.assignedShelfPrefab.name}");
            return;
        }

        var spawned = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
        spawned.name = $"Shelf_{spawnPoint.name}_{prefabToSpawn.name}";

        GameNetworkBridge.Instance?.ServerSpawn(spawned);

        foreach (var area in spawned.GetComponentsInChildren<StorageArea>())
        {
            area.scaleOffset = spawned.transform.localScale;
            break;
        }
    }

    private void SpawnRandomEmptyShelf(ShelfSpawn spawnPoint)
    {
        var candidates = shelfStorages
            .Where(s => s.allowedItemIDs.Count == 0 || s.allowedItemIDs.Contains(0))
            .ToList();

        if (candidates.Count == 0)
            candidates = shelfStorages.ToList();

        if (candidates.Count == 0) return;

        var area = candidates[Random.Range(0, candidates.Count)];
        var prefabToSpawn = GetRootPrefabForStorageArea(area);
        if (prefabToSpawn == null) return;

        var spawned = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
        spawned.name = $"EmptyShelf_{spawnPoint.name}_{prefabToSpawn.name}";

        GameNetworkBridge.Instance?.ServerSpawn(spawned);
        spawned.SetLayerRecursively(LayerMask.NameToLayer("Grass"));
    }

    #endregion

    #region Helpers

    private GameObject GetRootPrefabForStorageArea(StorageArea area)
    {
        if (storageAreaToPrefabMap.TryGetValue(area, out var mapped))
            return mapped;

        foreach (var prefab in shelfPrefabs)
        {
            if (prefab == null) continue;
            if (prefab.GetComponentsInChildren<StorageArea>(true).Contains(area))
                return prefab;
        }
        return null;
    }

    #endregion
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelvesStockManager : MonoBehaviour
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

    // Dictionary to map StorageArea back to its root prefab GameObject
    private Dictionary<StorageArea, GameObject> storageAreaToPrefabMap = new();

    public void UpdateShelfStoragesFromPrefabs()
    {
        shelfStorages.Clear();
        storageAreaToPrefabMap.Clear();

        if (shelfPrefabs?.Count > 0)
        {
            foreach (GameObject prefab in shelfPrefabs)
            {
                if (prefab == null) continue;

                StorageArea[] areas = prefab.GetComponentsInChildren<StorageArea>(true);
                foreach (StorageArea area in areas)
                {
                    if (!shelfStorages.Contains(area))
                    {
                        shelfStorages.Add(area);
                        // Map the StorageArea back to its root prefab
                        storageAreaToPrefabMap[area] = prefab;
                    }
                }
            }
        }
    }

    public void Work()
    {
        // Use GameManager's seed if available, otherwise use system time
        assignmentSeed = gameManager != null ? gameManager.levelSeed : (int)System.DateTime.Now.Ticks;
        Random.InitState(assignmentSeed);

        Debug.Log($"[ShelfManager] Using seed: {assignmentSeed} for assignment");

        UpdateShelfStoragesFromPrefabs();
        List<Item> availableItems = new List<Item>(gameManager.itemTemplates); // Create a copy to avoid modification issues

        // Filter only active spawn points
        var activeSpawns = shelfSpawns.Where(sp => sp.IsActiveForAssignment()).ToList();

        Dictionary<int, List<StorageArea>> shelfsByItemType = OrganizeShelvesbyItemType();

        List<Item> unassignedItems = AssignItemsToShelves(availableItems, shelfsByItemType, activeSpawns);

        // Keep unassigned items but don't spawn shelves for them
        // We're not deleting them anymore - they just won't be placed
        Debug.Log($"[ShelfManager] {unassignedItems.Count} items unassigned");

        foreach (Item item in unassignedItems)
        {
            gameManager.itemTemplates.Remove(item);
            Debug.Log($"Asking game manager to remove {item.name}.");
        }

        SpawnShelvesAtSpawnPoints(activeSpawns);
    }

    private Dictionary<int, List<StorageArea>> OrganizeShelvesbyItemType()
    {
        Dictionary<int, List<StorageArea>> shelfsByItemType = new Dictionary<int, List<StorageArea>>();

        foreach (StorageArea shelf in shelfStorages)
        {
            if (shelf.allowedItemIDs.Count == 0)
            {
                if (!shelfsByItemType.ContainsKey(-1))
                    shelfsByItemType[-1] = new List<StorageArea>();
                shelfsByItemType[-1].Add(shelf);
            }
            else
            {
                foreach (int itemID in shelf.allowedItemIDs)
                {
                    if (!shelfsByItemType.ContainsKey(itemID))
                        shelfsByItemType[itemID] = new List<StorageArea>();
                    shelfsByItemType[itemID].Add(shelf);
                }
            }
        }

        return shelfsByItemType;
    }

    private List<Item> AssignItemsToShelves(List<Item> items, Dictionary<int, List<StorageArea>> shelfsByItemType, List<ShelfSpawn> spawnPoints)
    {
        List<Item> unassignedItems = new List<Item>();
        // Only work with items that have a valid ID
        var validItems = items.Where(item => item.ID > 0).ToList();

        // 1. First, assign items to spawn points that specifically require them
        // 2. Then, assign remaining items to spawn points that accept wildcards
        // 3. Leave any extra spawn points empty (for red herrings)

        // Make copies so we can modify them during assignment
        var remainingSpawns = new List<ShelfSpawn>(spawnPoints);
        var remainingItems = new List<Item>(validItems);

        // STEP 1: Priority assignment - specific item to specific shelf
        foreach (var item in ShuffleList(validItems))
        {
            var compatibleSpawns = remainingSpawns.Where(spawn =>
                shelfsByItemType.ContainsKey(item.ID) &&
                shelfsByItemType[item.ID].Any(shelf => spawn.CanAcceptShelfType(shelf.shelfTypeID))
            ).ToList();

            if (compatibleSpawns.Count > 0)
            {
                // Select random compatible spawn
                var spawn = compatibleSpawns[Random.Range(0, compatibleSpawns.Count)];

                // Get compatible shelves for this spawn & item
                var compatibleShelves = shelfStorages.Where(shelf =>
                    shelf.allowedItemIDs.Contains(item.ID) &&
                    spawn.CanAcceptShelfType(shelf.shelfTypeID)
                ).ToList();

                // Select random compatible shelf
                var shelf = compatibleShelves.Count > 0 ?
                    compatibleShelves[Random.Range(0, compatibleShelves.Count)] :
                    null;

                if (shelf != null)
                {
                    int stockAmount = Random.Range(minInitialStock, maxInitialStock + 1);
                    spawn.AssignItemAndShelf(item.ID, shelf, stockAmount);

                    remainingSpawns.Remove(spawn);
                    remainingItems.Remove(item);
                    Debug.Log($"[ShelfManager] Assigned item {item.name} (ID:{item.ID}) to {spawn.name} with specific shelf");
                }
            }
        }

        // STEP 2: Assign remaining items to wildcard shelves
        var wildcardShelves = shelfStorages.Where(shelf => shelf.allowedItemIDs.Count == 0).ToList();

        foreach (var item in ShuffleList(remainingItems.ToList()))
        {
            var compatibleSpawns = remainingSpawns.Where(spawn =>
                wildcardShelves.Any(shelf => spawn.CanAcceptShelfType(shelf.shelfTypeID))
            ).ToList();

            if (compatibleSpawns.Count > 0)
            {
                // Select random compatible spawn
                var spawn = compatibleSpawns[Random.Range(0, compatibleSpawns.Count)];

                // Get wildcard shelves compatible with this spawn
                var compatibleShelves = wildcardShelves.Where(shelf =>
                    spawn.CanAcceptShelfType(shelf.shelfTypeID)
                ).ToList();

                if (compatibleShelves.Count > 0)
                {
                    // Select random wildcard shelf
                    var shelf = compatibleShelves[Random.Range(0, compatibleShelves.Count)];
                    int stockAmount = Random.Range(minInitialStock, maxInitialStock + 1);

                    spawn.AssignItemAndShelf(item.ID, shelf, stockAmount);

                    remainingSpawns.Remove(spawn);
                    remainingItems.Remove(item);
                    Debug.Log($"[ShelfManager] Assigned item {item.name} (ID:{item.ID}) to {spawn.name} with wildcard shelf");
                }
            }
        }

        // Any items left are unassigned (more items than spawn points)
        unassignedItems = remainingItems;

        // At this point, all available spawn points have either been assigned items or will remain empty
        return unassignedItems;
    }

    // Helper to shuffle lists for random assignment
    private List<T> ShuffleList<T>(List<T> list)
    {
        List<T> shuffled = new List<T>(list);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffled.Count);
            T temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }
        return shuffled;
    }

    private void SpawnShelvesAtSpawnPoints(List<ShelfSpawn> spawnPoints)
    {
        foreach (ShelfSpawn spawnPoint in spawnPoints)
        {
            if (spawnPoint.IsAssigned())
            {
                // Get the root prefab GameObject that contains this StorageArea
                GameObject prefabToSpawn = GetRootPrefabForStorageArea(spawnPoint.assignedShelfPrefab);

                if (prefabToSpawn != null)
                {
                    GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                    spawnedShelf.name = $"Shelf_{spawnPoint.name}_{prefabToSpawn.name}";

                    // Find and configure the StorageArea component in the spawned shelf
                    StorageArea[] storageAreas = spawnedShelf.GetComponentsInChildren<StorageArea>();
                    foreach (var area in storageAreas)
                    {
                        if (area.shelfTypeID == spawnPoint.assignedShelfPrefab.shelfTypeID)
                        {
                            area.assignedItemID = spawnPoint.assignedItemID;
                            area.itemAmount = spawnPoint.assignedItemAmount;
                            area.scaleOffset = spawnedShelf.transform.localScale;
                            area.CreateVisualStock();
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Could not find root prefab for StorageArea {spawnPoint.assignedShelfPrefab.name}");
                }
            }
            else
            {
                // This is critical: Spawn empty shelves for red herrings!
                // Find any wildcard shelf that fits this spawn point
                var wildcardShelves = shelfStorages
                    .Where(shelf => shelf.allowedItemIDs.Count == 0 &&
                                    spawnPoint.CanAcceptShelfType(shelf.shelfTypeID))
                    .ToList();

                if (wildcardShelves.Count > 0)
                {
                    StorageArea emptyShelf = wildcardShelves[Random.Range(0, wildcardShelves.Count)];

                    GameObject prefabToSpawn = GetRootPrefabForStorageArea(emptyShelf);
                    if (prefabToSpawn != null)
                    {
                        GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                        spawnedShelf.name = $"EmptyShelf_{spawnPoint.name}_{prefabToSpawn.name}";
                        spawnedShelf.SetLayerRecursively(LayerMask.NameToLayer("Grass"));

                        // Find and configure the StorageArea for empty shelf
                        StorageArea[] storageAreas = spawnedShelf.GetComponentsInChildren<StorageArea>();
                        foreach (var area in storageAreas)
                        {
                            area.assignedItemID = 0; // No item
                            area.itemAmount = 0;     // Empty shelf
                            area.enabled = false;    // Brain off
                        }
                        // TODO: FUCK SHIT FUCK SHIT ITS EATING THE FUCKING DUCKS?!!!!!!!!!! FUCK !!!!!!!!!!!!!!!!!
                        Debug.Log($"[ShelfManager] Spawning empty shelf at {spawnPoint.name} for red herring");
                    }
                    else
                    {
                        Debug.LogError($"Could not find root prefab for wildcard shelf at {spawnPoint.name}");
                    }
                }
                else
                {
                    // If no wildcard shelves, use any shelf that fits the spawn requirements
                    var compatibleShelves = shelfStorages
                        .Where(shelf => spawnPoint.CanAcceptShelfType(shelf.shelfTypeID))
                        .ToList();

                    if (compatibleShelves.Count > 0)
                    {
                        StorageArea emptyShelf = compatibleShelves[Random.Range(0, compatibleShelves.Count)];

                        GameObject prefabToSpawn = GetRootPrefabForStorageArea(emptyShelf);
                        if (prefabToSpawn != null)
                        {
                            GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                            spawnedShelf.name = $"EmptyShelf_{spawnPoint.name}_{prefabToSpawn.name}";
                            spawnedShelf.SetLayerRecursively(LayerMask.NameToLayer("Grass"));

                            StorageArea[] storageAreas = spawnedShelf.GetComponentsInChildren<StorageArea>();
                            foreach (var area in storageAreas)
                            {
                                if (area.shelfTypeID == emptyShelf.shelfTypeID)
                                {
                                    area.assignedItemID = 0;
                                    area.itemAmount = 0;
                                    break;
                                }
                            }

                            Debug.Log($"[ShelfManager] Spawning empty shelf at {spawnPoint.name} for red herring");
                        }
                    }
                }
            }
        }
    }

    private GameObject GetRootPrefabForStorageArea(StorageArea storageArea)
    {
        if (storageAreaToPrefabMap.TryGetValue(storageArea, out GameObject mappedPrefab))
        {
            return mappedPrefab;
        }

        foreach (GameObject prefab in shelfPrefabs)
        {
            if (prefab == null) continue;

            StorageArea[] areas = prefab.GetComponentsInChildren<StorageArea>(true);
            if (areas.Contains(storageArea))
            {
                return prefab;
            }
        }

        return null;
    }

    // Add this method to clean up assignments if needed
    public void ResetAssignments()
    {
        foreach (var spawn in shelfSpawns)
        {
            spawn.ResetAssignment();
        }
    }
}

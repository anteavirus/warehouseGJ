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

    [Tooltip("Hi I am storageAreas from shelfPrefabs")]
    public List<StorageArea> shelfStorages;

    // Dictionary to map StorageArea back to its root prefab GameObject
    private Dictionary<StorageArea, GameObject> storageAreaToPrefabMap = new Dictionary<StorageArea, GameObject>();

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
        UpdateShelfStoragesFromPrefabs();
        List<Item> availableItems = gameManager.itemTemplates;
        Dictionary<int, List<StorageArea>> shelfsByItemType = OrganizeShelvesbyItemType();

        List<Item> unassignedItems = AssignItemsToShelves(availableItems, shelfsByItemType);

        DeleteUnassignedItems(unassignedItems);

        SpawnShelvesAtSpawnPoints();

        GenerateInitialStock();
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

    private List<Item> AssignItemsToShelves(List<Item> items, Dictionary<int, List<StorageArea>> shelfsByItemType)
    {
        List<Item> unassignedItems = new();

        foreach (Item item in items)
        {
            List<StorageArea> compatibleShelves = new();

            if (shelfsByItemType.ContainsKey(item.ID))
            {
                compatibleShelves.AddRange(shelfsByItemType[item.ID]);
            }

            if (shelfsByItemType.ContainsKey(-1))
            {
                compatibleShelves.AddRange(shelfsByItemType[-1]);
            }

            if (compatibleShelves.Count > 0)
            {
                StorageArea chosenShelf = compatibleShelves[Random.Range(0, compatibleShelves.Count)];
                chosenShelf.assignedItemID = item.ID;
                chosenShelf.itemAmount = Random.Range(minInitialStock, maxInitialStock + 1);
            }
            else
            {
                unassignedItems.Add(item);
            }
        }

        return unassignedItems;
    }

    private void DeleteUnassignedItems(List<Item> unassignedItems)
    {
        foreach (Item item in unassignedItems)
        {
            Debug.LogWarning($"No storage available for item {item.name} (ID: {item.ID}).");
            gameManager.itemTemplates.Remove(item);
        }
    }

    private void SpawnShelvesAtSpawnPoints()
    {
        foreach (ShelfSpawn spawnPoint in shelfSpawns)
        {
            List<StorageArea> compatibleShelves = GetShelvesCompatibleWithSpawnPoint(spawnPoint);

            if (compatibleShelves.Count > 0)
            {
                StorageArea shelfToSpawn = compatibleShelves[Random.Range(0, compatibleShelves.Count)];

                // Get the root prefab GameObject that contains this StorageArea
                GameObject prefabToSpawn = GetRootPrefabForStorageArea(shelfToSpawn);

                if (prefabToSpawn != null)
                {
                    GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                    spawnedShelf.name = $"Shelf_{spawnPoint}_{prefabToSpawn.name}";
                }
                else
                {
                    Debug.LogError($"Could not find root prefab for StorageArea {shelfToSpawn.name}");
                }
            }
            else
            {
                Debug.LogWarning($"No compatible shelves found for spawn point {spawnPoint}");
            }
        }
    }

    // Method to get the root prefab GameObject for a given StorageArea
    private GameObject GetRootPrefabForStorageArea(StorageArea storageArea)
    {
        // First check our mapping dictionary
        if (storageAreaToPrefabMap.TryGetValue(storageArea, out GameObject mappedPrefab))
        {
            return mappedPrefab;
        }

        // Fallback: search through shelfPrefabs to find which one contains this StorageArea
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

    private List<StorageArea> GetShelvesCompatibleWithSpawnPoint(ShelfSpawn spawnPoint)
    {
        return shelfStorages.Where(shelf =>
            spawnPoint.acceptedShelfTypes.Any(acceptedShelfID => acceptedShelfID == shelf.shelfTypeID)
        ).ToList();
    }

    private void GenerateInitialStock()
    {
        foreach (StorageArea shelf in shelfStorages)
        {
            if (shelf.assignedItemID != 0 && shelf.itemAmount > 0)
            {
                shelf.CreateVisualStock();
            }
        }
    }
}

using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    // Dictionary to map StorageArea back to its root prefab GameObject
    private Dictionary<StorageArea, GameObject> storageAreaToPrefabMap = new();

    public override void Initialize()
    {
        base.Initialize();
        if (gameManager == null)
            gameManager = (GameManager.Instance);
    }

    public void Initialize(GameManager gm)
    {
        base.Initialize();
        gameManager = gm;
        if (shelfSpawns.All(x => x == null))
        {
            shelfSpawns = FindObjectsOfType<ShelfSpawn>().ToList();
        }
        // todo. m. a thing.
    }

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
        var activeSpawns = shelfSpawns.Where(sp => sp != null && sp.IsActiveForAssignment()).ToList();

        //foreach (var item in activeSpawns) item.ResetAssignment(); // Reset, shitcoded gizmos might've fucked it all up. TODO: create a better system // Or, is it?

        // Keep unassigned items but don't spawn shelves for them
        // We're not deleting them anymore - they just won't be placed
        SpawnShelvesAtSpawnPoints(activeSpawns);
    }

    private void SpawnShelvesAtSpawnPoints(List<ShelfSpawn> spawnPoints)
    {
        foreach (ShelfSpawn spawnPoint in spawnPoints)
        {
            if (spawnPoint.IsAssigned())
            {
                GameObject prefabToSpawn = GetRootPrefabForStorageArea(spawnPoint.assignedShelfPrefab);

                if (prefabToSpawn != null)
                {
                    GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                    spawnedShelf.name = $"Shelf_{spawnPoint.name}_{prefabToSpawn.name}";

                    if (isServer)
                    {
                        uint assetId = prefabToSpawn.GetComponent<NetworkIdentity>().assetId;
                        Debug.LogError($"Spawning shelf {prefabToSpawn.name} with assetId {assetId}");
                    }

                    //if (spawnedShelf.GetComponent<NetworkIdentity>() == null)
                    //    spawnedShelf.AddComponent<NetworkIdentity>();
                    //if (spawnedShelf.GetComponent<NetworkTransformReliable>() == null)
                    //    spawnedShelf.AddComponent<NetworkTransformReliable>();

                    NetworkServer.Spawn(spawnedShelf);

                    // Find and configure the StorageArea component in the spawned shelf
                    StorageArea[] storageAreas = spawnedShelf.GetComponentsInChildren<StorageArea>();
                    foreach (var area in storageAreas)
                    {
                        // In the new StorageArea, we don't assign item IDs or amounts
                        // The shelf just accepts items based on its allowedItemIDs
                        area.scaleOffset = spawnedShelf.transform.localScale;
                        break;
                    }
                }
                else
                {
                    Debug.LogError($"Could not find root prefab for StorageArea {spawnPoint.assignedShelfPrefab.name}");
                }
            }
            else
            {
                var wildcardShelves = shelfStorages
                    .Where(shelf => shelf.allowedItemIDs.Count == 0 || shelf.allowedItemIDs.Contains(0))
                    .ToList();

                if (wildcardShelves.Count > 0)
                {
                    StorageArea emptyShelf = wildcardShelves[Random.Range(0, wildcardShelves.Count)];

                    GameObject prefabToSpawn = GetRootPrefabForStorageArea(emptyShelf);
                    if (prefabToSpawn != null)
                    {
                        GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                        if (isServer)
                        {
                            uint assetId = prefabToSpawn.GetComponent<NetworkIdentity>().assetId;
                            Debug.LogError($"Spawning shelf {prefabToSpawn.name} with assetId {assetId}");
                        }
                        //if (spawnedShelf.GetComponent<NetworkIdentity>() == null)
                        //    spawnedShelf.AddComponent<NetworkIdentity>();
                        //if (spawnedShelf.GetComponent<NetworkTransformReliable>() == null)
                        //    spawnedShelf.AddComponent<NetworkTransformReliable>();

                        NetworkServer.Spawn(spawnedShelf);
                        spawnedShelf.name = $"EmptyShelf_{spawnPoint.name}_{prefabToSpawn.name}";
                        spawnedShelf.SetLayerRecursively(LayerMask.NameToLayer("Grass"));
                    }
                }
                else
                {
                    if (shelfStorages.Count > 0)
                    {
                        StorageArea emptyShelf = shelfStorages[Random.Range(0, shelfStorages.Count)];

                        GameObject prefabToSpawn = GetRootPrefabForStorageArea(emptyShelf);
                        if (prefabToSpawn != null)
                        {
                            GameObject spawnedShelf = Instantiate(prefabToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                            if (isServer)
                            {
                                uint assetId = prefabToSpawn.GetComponent<NetworkIdentity>().assetId;
                                Debug.LogError($"Spawning shelf {prefabToSpawn.name} with assetId {assetId}");
                            }
                            //if (spawnedShelf.GetComponent<NetworkIdentity>() == null)
                            //    spawnedShelf.AddComponent<NetworkIdentity>();
                            //if (spawnedShelf.GetComponent<NetworkTransformReliable>() == null)
                            //    spawnedShelf.AddComponent<NetworkTransformReliable>();

                            NetworkServer.Spawn(spawnedShelf);
                            spawnedShelf.name = $"EmptyShelf_{spawnPoint.name}_{prefabToSpawn.name}";
                            spawnedShelf.SetLayerRecursively(LayerMask.NameToLayer("Grass"));
                        }
                    }
                }
            }
        }
    }

    // TODO: latest update 14.02.26; this fucking shit sucks what the fuck why the fuck the fucking delivery areas in the prefab have their own ids?????????? and why the fuck must i . do i HAVE to split the fucking stupid shit in two parts? like in "gameplay" and "static" shit???????? this fucking sucksssssssssssssss mannnnn fuck me for thinking multiplayer would be easy enough to implement ughhhhhhhhhhhhhhhhhhhhhhh and i'll need to GPT my fucuking dossier or whatever to pass onto the next month and at the end of april  make the fucking game work.  man i should've went into papers at least i wouldn't hate the shit out of programming now

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
}
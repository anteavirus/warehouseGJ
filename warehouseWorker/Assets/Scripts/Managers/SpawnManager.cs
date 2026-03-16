using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class SpawnManager : GenericManager<SpawnManager>
{
    [Tooltip("Map of custom names to prefabs. If left empty, it will auto-populate from NetworkManager's spawnPrefabs on Start.")]
    public Dictionary<string, GameObject> spawnableObjects = new Dictionary<string, GameObject>();

    public override void Initialize()
    {
        if (Instance == null)
        {
            Instance = this;
            if (gameObject.scene.name != "DontDestroyOnLoad")
                DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log(netIdentity);  // Are they all really null?

        if (spawnableObjects.Count == 0)
        {
            RefreshFromNetworkManager();
        }
    }

    /// <summary>
    /// Populates the dictionary from NetworkManager's spawnPrefabs list.
    /// Duplicate keys are skipped (warning logged).
    /// </summary>
    public void RefreshFromNetworkManager()
    {
        var networkManager = NetworkManager.singleton ?? NetworkGameManager.Instance ?? null;
        if (networkManager == null)
        {
            Debug.LogError("Network Manager is null, tried both the defaul NetworkManager.singleton and NetworkGameManager.Instance, because of course we have to do our things that'll interfere with the prebuilt ones, AnTeaVirus. That's clearly a smart thing to do.");
            return;
        }

        spawnableObjects.Clear();

        foreach (var prefab in networkManager.spawnPrefabs)
        {
            if (prefab == null) continue;

            string key = prefab.name;
            if (!spawnableObjects.ContainsKey(key))
            {
                spawnableObjects.Add(key, prefab);
            }
            else
            {
                Debug.LogWarning($"Duplicate prefab name '{key}' ignored when populating spawnableObjects.");
            }
        }
    }

    /// <summary>
    /// Checks if a prefab is registered in NetworkManager's spawnPrefabs list.
    /// </summary>
    public bool IsPrefabRegistered(GameObject prefab)
    {
        if (NetworkManager.singleton == null) return false;
        return NetworkManager.singleton.spawnPrefabs.Contains(prefab);
    }

    // ==================== SPAWN BY NAME ====================

    /// <summary>
    /// Spawns a networked object by name (key in spawnableObjects dictionary).
    /// </summary>
    public GameObject SpawnObject(string name, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("SpawnObject can only be called on an active server.");
            return null;
        }

        if (netIdentity == null)
        {
            Debug.LogError("NetIdentity is null. Can someone replace this coder? Clearly this one is incapable of doing anything.");
            return null;
        }

        if (isClient)
        {
            Debug.LogError("Client attempted to network spawn an object.");
            return null;
        }

        if (!spawnableObjects.TryGetValue(name, out GameObject prefab))
        {
            Debug.LogError($"No prefab found with key '{name}'. Available keys: {string.Join(", ", spawnableObjects.Keys)}");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        NetworkServer.Spawn(instance);
        return instance;
    }

    public GameObject SpawnObject(string name, Vector3 position, Transform parent = null)
    {
        return SpawnObject(name, position, Quaternion.identity, parent);
    }

    public GameObject SpawnObject(string name)
    {
        return SpawnObject(name, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Spawns a networked object by name with a specific connection (ownership).
    /// </summary>
    public GameObject SpawnObject(string name, Vector3 position, Quaternion rotation, NetworkConnection conn)
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("SpawnObject can only be called on an active server.");
            return null;
        }

        if (netIdentity == null)
        {
            Debug.LogError("NetIdentity is null. Can someone replace this coder? Clearly this one is incapable of doing anything.");
            return null;
        }

        if (isClient)
        {
            Debug.LogError("Client attempted to network spawn an object.");
            return null;
        }

        if (!spawnableObjects.TryGetValue(name, out GameObject prefab))
        {
            Debug.LogError($"No prefab found with key '{name}'.");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(instance, conn.identity.gameObject);
        return instance;
    }

    // ==================== SPAWN BY PREFAB ====================

    /// <summary>
    /// Spawns a networked object directly from a prefab reference.
    /// The prefab must be registered in NetworkManager's spawnPrefabs list.
    /// </summary>
    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("SpawnObject can only be called on an active server.");
            return null;
        }

        if (netIdentity == null)
        {
            Debug.LogError("NetIdentity is null. Can someone replace this coder? Clearly this one is incapable of doing anything.");
            return null;
        }

        if (isClient)
        {
            Debug.LogError("Client attempted to network spawn an object.");
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("Cannot spawn a null prefab.");
            return null;
        }

        if (!IsPrefabRegistered(prefab))
        {
            Debug.LogError($"Prefab '{prefab.name}' is not registered in NetworkManager's spawnPrefabs list. Add it to the list in the NetworkManager inspector.");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        NetworkServer.Spawn(instance);
        return instance;
    }

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Transform parent = null)
    {
        return SpawnObject(prefab, position, Quaternion.identity, parent);
    }

    public GameObject SpawnObject(GameObject prefab)
    {
        return SpawnObject(prefab, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Spawns a networked object from a prefab with a specific connection (ownership).
    /// </summary>
    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection conn)
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("SpawnObject can only be called on an active server.");
            return null;
        }

        if (netIdentity == null)
        {
            Debug.LogError("NetIdentity is null. Can someone replace this coder? Clearly this one is incapable of doing anything.");
            return null;
        }

        if (isClient)
        {
            Debug.LogError("Client attempted to network spawn an object.");
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("Cannot spawn a null prefab.");
            return null;
        }

        if (!IsPrefabRegistered(prefab))
        {
            Debug.LogError($"Prefab '{prefab.name}' is not registered in NetworkManager's spawnPrefabs list.");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(instance, conn.identity.gameObject);
        return instance;
    }
}
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Spawns networked objects by name or prefab reference.
/// Pure MonoBehaviour — uses <see cref="GameNetworkBridge"/> for
/// server checks and network spawning.
/// </summary>
public class SpawnManager : GenericManager<SpawnManager>
{
    [Tooltip("Map of custom names to prefabs. Auto-populated from NetworkManager.spawnPrefabs if empty.")]
    public Dictionary<string, GameObject> spawnableObjects = new Dictionary<string, GameObject>();

    #region Bridge shortcut

    private bool IsServer => GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isServer;
    private bool IsClient => GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isClient;

    #endregion

    #region Initialize

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

        if (spawnableObjects.Count == 0)
            RefreshFromNetworkManager();
    }

    #endregion

    #region Prefab registry

    /// <summary>Populates dictionary from NetworkManager.spawnPrefabs.</summary>
    public void RefreshFromNetworkManager()
    {
        var nm = NetworkManager.singleton ?? NetworkGameManager.Instance;
        if (nm == null)
        {
            Debug.LogError("NetworkManager is null — tried singleton and NetworkGameManager.Instance.");
            return;
        }

        spawnableObjects.Clear();
        foreach (var prefab in nm.spawnPrefabs)
        {
            if (prefab == null) continue;
            string key = prefab.name;
            if (!spawnableObjects.ContainsKey(key))
                spawnableObjects.Add(key, prefab);
            else
                Debug.LogWarning($"Duplicate prefab name '{key}' ignored.");
        }
    }

    public bool IsPrefabRegistered(GameObject prefab)
    {
        return NetworkManager.singleton != null && NetworkManager.singleton.spawnPrefabs.Contains(prefab);
    }

    #endregion

    #region Spawn by name

    public GameObject SpawnObject(string name, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!IsServer) { Debug.LogError("SpawnObject can only be called on server."); return null; }

        if (!spawnableObjects.TryGetValue(name, out var prefab))
        {
            Debug.LogError($"No prefab '{name}'. Available: {string.Join(", ", spawnableObjects.Keys)}");
            return null;
        }

        var instance = Instantiate(prefab, position, rotation, parent);
        GameNetworkBridge.Instance?.ServerSpawn(instance);
        return instance;
    }

    public GameObject SpawnObject(string name, Vector3 position, Transform parent = null)
        => SpawnObject(name, position, Quaternion.identity, parent);

    public GameObject SpawnObject(string name)
        => SpawnObject(name, Vector3.zero, Quaternion.identity);

    public GameObject SpawnObject(string name, Vector3 position, Quaternion rotation, NetworkConnection conn)
    {
        if (!IsServer) { Debug.LogError("SpawnObject can only be called on server."); return null; }

        if (!spawnableObjects.TryGetValue(name, out var prefab))
        {
            Debug.LogError($"No prefab '{name}'.");
            return null;
        }

        var instance = Instantiate(prefab, position, rotation);
        if (conn?.identity?.gameObject != null)
            GameNetworkBridge.Instance?.ServerSpawn(instance, conn.identity.gameObject);
        else
            GameNetworkBridge.Instance?.ServerSpawn(instance);
        return instance;
    }

    #endregion

    #region Spawn by prefab

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!IsServer) { Debug.LogError("SpawnObject can only be called on server."); return null; }
        if (prefab == null) { Debug.LogError("Cannot spawn null prefab."); return null; }
        if (!IsPrefabRegistered(prefab))
        {
            Debug.LogError($"Prefab '{prefab.name}' not in NetworkManager.spawnPrefabs.");
            return null;
        }

        var instance = Instantiate(prefab, position, rotation, parent);
        GameNetworkBridge.Instance?.ServerSpawn(instance);
        return instance;
    }

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Transform parent = null)
        => SpawnObject(prefab, position, Quaternion.identity, parent);

    public GameObject SpawnObject(GameObject prefab)
        => SpawnObject(prefab, Vector3.zero, Quaternion.identity);

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection conn)
    {
        if (!IsServer) { Debug.LogError("SpawnObject can only be called on server."); return null; }
        if (prefab == null) { Debug.LogError("Cannot spawn null prefab."); return null; }
        if (!IsPrefabRegistered(prefab))
        {
            Debug.LogError($"Prefab '{prefab.name}' not in NetworkManager.spawnPrefabs.");
            return null;
        }

        var instance = Instantiate(prefab, position, rotation);
        if (conn?.identity?.gameObject != null)
            GameNetworkBridge.Instance?.ServerSpawn(instance, conn.identity.gameObject);
        else
            GameNetworkBridge.Instance?.ServerSpawn(instance);
        return instance;
    }

    #endregion
}

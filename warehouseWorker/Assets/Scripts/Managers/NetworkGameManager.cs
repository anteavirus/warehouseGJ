using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using kcp2k;

/// <summary>
/// Mirror NetworkManager subclass. Owns player spawning, scene management,
/// and creates the <see cref="GameNetworkBridge"/> on server start.
/// This is the ONLY NetworkManager — everything else is MonoBehaviour.
/// </summary>
public class NetworkGameManager : NetworkManager
{
    public static NetworkGameManager Instance { get; private set; }

    #region Inspector fields

    [Header("Player Spawning")]
    public Transform[] spawnPoints;
    private int nextSpawnIndex = 0;

    [Header("Game Settings")]
    public int maxPlayers = 4;

    [Header("Prefabs")]
    public GameObject gameStatePrefab;
    public GameObject gameNetworkBridgePrefab;   // NEW — must be assigned
    public GameObject endlessGamemodeTimerPrefab;
    public GameObject shiftsGamemodeTimerPrefab;

    #endregion

    #region Runtime state

    private NetworkGameState gameState;
    private Dictionary<int, GameObject> playerGameObjects = new Dictionary<int, GameObject>();

    #endregion

    #region Lifecycle

    public override void Awake()
    {
        base.Awake();
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Server start / stop

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[NetworkGameManager] Server started.");

        // 1. Ensure MasterManager exists
        if (MasterManager.Instance == null)
        {
            var mm = FindObjectOfType<MasterManager>();
            if (mm == null)
                mm = Instantiate(Resources.Load<MasterManager>("Prefabs/MasterManager"));
            mm.Initialize();
        }
        // Spawn master manager on network so clients can find it
        NetworkServer.Spawn(MasterManager.Instance.gameObject);

        // 2. Create & spawn GameNetworkBridge
        if (GameNetworkBridge.Instance == null && gameNetworkBridgePrefab != null)
        {
            var bridgeObj = Instantiate(gameNetworkBridgePrefab, transform);
            bridgeObj.transform.SetParent(transform);
            bridgeObj.name = "GameNetworkBridge";
            NetworkServer.Spawn(bridgeObj);
            Debug.Log("[NetworkGameManager] GameNetworkBridge created & spawned.");
        }
        else if (GameNetworkBridge.Instance == null)
        {
            Debug.LogError("[NetworkGameManager] gameNetworkBridgePrefab not assigned! " +
                           "Cannot create the network bridge. Multiplayer will break.");
        }

        // 3. Spawn game state
        if (gameStatePrefab != null)
        {
            var gsObj = Instantiate(gameStatePrefab);
            gsObj.transform.SetParent(transform);
            gameState = gsObj.GetComponent<NetworkGameState>();
            NetworkServer.Spawn(gsObj);
            gameState?.SetGameStatus(GameStatus.Lobby);
        }

        // 4. Initialize managers (they will now find the bridge)
        if (MasterManager.Instance != null)
            MasterManager.Instance.Initialize();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[NetworkGameManager] Server stopped.");
        playerGameObjects.Clear();
    }

    #endregion

    #region Client connect / disconnect / scene

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[NetworkGameManager] Client connected.");
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        string sceneName = SceneManager.GetActiveScene().name;

        if (NetworkClient.isConnected && !NetworkServer.active && sceneName != "Main Menu")
        {
            StartCoroutine(InitClientAfterSceneLoad());
        }
    }

    private System.Collections.IEnumerator InitClientAfterSceneLoad()
    {
        yield return new WaitUntil(() => NetworkClient.isLoadingScene == false);

        var master = FindObjectOfType<MasterManager>();
        if (master != null)
        {
            master.Initialize();
            Debug.Log("[NetworkGameManager] Client: managers initialized after scene load.");
        }
        else
        {
            Debug.LogWarning("[NetworkGameManager] Client: MasterManager not found in scene.");
        }
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[NetworkGameManager] Client disconnected.");
        if (SceneManager.GetActiveScene().name != "Main Menu")
            SceneManager.LoadScene("Main Menu");
    }

    #endregion

    #region Player management

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxPlayers)
        {
            Debug.LogWarning($"Max players ({maxPlayers}) reached. Rejecting connection.");
            conn.Disconnect();
            return;
        }

        Vector3 spawnPos = GetNextSpawnPosition();
        var player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        player.transform.SetParent(transform);
        NetworkServer.AddPlayerForConnection(conn, player);

        DisableYoShit(player);
        playerGameObjects[conn.connectionId] = player;

        gameState?.UpdatePlayerCount(numPlayers);
        Debug.Log($"[NetworkGameManager] Player {conn.connectionId} spawned at {spawnPos}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (playerGameObjects.TryGetValue(conn.connectionId, out var go))
        {
            NetworkServer.UnSpawn(go);
            playerGameObjects.Remove(conn.connectionId);
        }
        gameState?.UpdatePlayerCount(numPlayers - 1);
        base.OnServerDisconnect(conn);
    }

    private Vector3 GetNextSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[nextSpawnIndex++ % spawnPoints.Length].position;

        float angle = (nextSpawnIndex++ * 90f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * 2f, 1f, Mathf.Sin(angle) * 2f);
    }

    #endregion

    #region Game flow

    /// <summary>Transitions from lobby to gameplay.</summary>
    [Server]
    public void BeginGame(int gameMode)
    {
        if (gameState == null) return;
        if (gameState.gameStatus != GameStatus.Lobby)
        {
            Debug.LogWarning("Cannot begin game — not in lobby.");
            return;
        }

        gameState.SetGameMode(gameMode);
        gameState.SetGameStatus(GameStatus.Ingame);
        ServerChangeScene(GetSceneNameForGameMode(gameMode));
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (sceneName != "Main Menu" && gameState != null && gameState.gameStatus == GameStatus.Ingame)
        {
            SetupGameMode(gameState.selectedGameMode);
            MasterManager.Instance?.Initialize();

            // Reposition players
            int idx = 0;
            foreach (var kvp in playerGameObjects)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.transform.position = GetSpawnPositionForIndex(idx++);
                    ReenableYoShit(kvp.Value);
                }
            }
        }
    }

    private string GetSceneNameForGameMode(int gameMode) => "GameplayScene";

    [Server]
    private void SetupGameMode(int gameMode)
    {
        if (MasterManager.Instance == null) return;

        var timerMan = MasterManager.Instance.transform.Find("TimerManager")?.gameObject;
        if (timerMan == null) return;

        foreach (Transform child in timerMan.transform)
        {
            if (child != timerMan.transform)
                Destroy(child.gameObject);
        }

        GameObject timerPrefab = gameMode switch
        {
            1 => shiftsGamemodeTimerPrefab,
            _ => endlessGamemodeTimerPrefab
        };

        if (timerPrefab == null) return;

        var timerInstance = Instantiate(timerPrefab, timerMan.transform);
        if (timerInstance.TryGetComponent<GenericTimer>(out var timer))
        {
            GameManager.Instance.timer = timer;
            timer.Initialize(GameManager.Instance);
            GameNetworkBridge.Instance?.ServerSpawn(timerInstance);
        }
    }

    #endregion

    #region Player enable/disable

    private void DisableYoShit(GameObject player)
    {
        if (player.TryGetComponent<PlayerController>(out var pc))
            pc.DisableYoShit(player);
    }

    private void ReenableYoShit(GameObject player)
    {
        if (player.TryGetComponent<PlayerController>(out var pc))
            pc.ReenableYoShit(player);
    }

    private Vector3 GetSpawnPositionForIndex(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[index % spawnPoints.Length].position;

        float angle = (index * 90f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * transform.position.x,
                           transform.position.y,
                           Mathf.Sin(angle) * transform.position.z);
    }

    #endregion

    #region Public UI-facing methods (called from buttons etc.)

    public void StartHostGame(string port = "7777")
    {
        if (string.IsNullOrEmpty(port)) { StartHost(); return; }

        var transport = Transport.active;
        if (transport is KcpTransport kcp)
        {
            if (ushort.TryParse(port, out var result) && result is >= 1 and <= 65535)
                kcp.Port = result;
            else
            {
                Debug.LogError("Port must be 1–65535.");
                return;
            }
        }
        else
        {
            Debug.LogError("No KcpTransport found.");
            return;
        }
        StartHost();
    }

    public void StartClientGame(string address = "localhost:7777")
    {
        var split = address.Split(':');
        if (split.Length < 2)
        {
            networkAddress = address;
        }
        else
        {
            networkAddress = split[0];
            var transport = Transport.active;
            if (transport is KcpTransport kcp)
            {
                if (ushort.TryParse(split[1], out var result) && result is >= 1 and <= 65535)
                    kcp.Port = result;
                else
                {
                    Debug.LogError("Port must be 1–65535.");
                    return;
                }
            }
            else
            {
                Debug.LogError("No KcpTransport found.");
                return;
            }
        }
        StartClient();
    }

    public void StopHostGame() => StopHost();
    public void StopClientGame() => StopClient();

    #endregion
}

using Mirror;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using kcp2k;

public class NetworkGameManager : NetworkManager
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Player Spawning")]
    public Transform[] spawnPoints;
    private int nextSpawnIndex = 0;

    [Header("Game Settings")]
    public int maxPlayers = 4;

    [Header("Game State Prefab")]
    public GameObject gameStatePrefab;

    [Header("Game Mode Prefabs")]
    public GameObject endlessGamemodeTimerPrefab;
    public GameObject shiftsGamemodeTimerPrefab;

    private NetworkGameState gameState;
    private Dictionary<int, GameObject> playerGameObjects = new Dictionary<int, GameObject>();

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
            return;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("Server started!");

        // Spawn game state object
        if (gameStatePrefab != null)
        {
            GameObject gameStateObj = Instantiate(gameStatePrefab);
            gameStateObj.transform.SetParent(transform);
            gameState = gameStateObj.GetComponent<NetworkGameState>();
            NetworkServer.Spawn(gameStateObj);
            
            if (gameState != null)
            {
                gameState.SetGameStatus(GameStatus.Lobby);
            }
        }

        // Initialize game managers on server
        if (MasterManager.Instance != null)
        {
            MasterManager.Instance.Initialize();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("Server stopped!");
        playerGameObjects.Clear();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("Client connected!");
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        string sceneName = SceneManager.GetActiveScene().name;
        // When client (not host) finishes loading the gameplay scene, initialize managers locally
        // so shelves and other systems run. Server already inits in OnServerSceneChanged.
        if (NetworkClient.isConnected && !NetworkServer.active && sceneName != "Main Menu")
        {
            StartCoroutine(InitializeClientManagersAfterSceneLoad());
        }
    }

    private System.Collections.IEnumerator InitializeClientManagersAfterSceneLoad()
    {
        // Wait a few frames so SyncVars (e.g. GameManager.levelSeed) have time to sync from server before shelf generation
        for (int i = 0; i < 3; i++)
            yield return null;
        var master = FindObjectOfType<MasterManager>();
        if (master != null)
        {
            master.Initialize();
            Debug.Log("Client: managers initialized after scene load.");
        }
        else
        {
            Debug.LogWarning("Client: MasterManager not found in scene.");
        }
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("Client disconnected!");

        // Return to main menu on disconnect
        if (SceneManager.GetActiveScene().name != "Main Menu")
        {
            SceneManager.LoadScene("Main Menu");
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxPlayers)
        {
            Debug.LogWarning($"Max players ({maxPlayers}) reached. Rejecting connection.");
            conn.Disconnect();
            return;
        }

        Vector3 spawnPos = GetNextSpawnPosition();
        Quaternion spawnRot = Quaternion.identity;

        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);
        player.transform.SetParent(transform);
        NetworkServer.AddPlayerForConnection(conn, player);
        NetworkServer.Spawn(player);
        DisableYoShit(player);

        playerGameObjects[conn.connectionId] = player;

        if (gameState != null)
        {
            gameState.UpdatePlayerCount(numPlayers);
        }

        //OrdersManager.Instance.SyncFullOrdersStateToPlayer(conn);  <-- shit, do this fucker somewhere... else
        Debug.Log($"Player {conn.connectionId} spawned at {spawnPos}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (playerGameObjects.ContainsKey(conn.connectionId))
        {
            playerGameObjects.Remove(conn.connectionId);
            NetworkServer.UnSpawn(playerGameObjects[conn.connectionId]);
        }

        if (gameState != null)
        {
            gameState.UpdatePlayerCount(numPlayers - 1);
        }

        base.OnServerDisconnect(conn);
    }

    private Vector3 GetNextSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
            nextSpawnIndex++;
            return spawnPoint.position;
        }

        // Fallback: spawn around origin
        float angle = (nextSpawnIndex * 90f) * Mathf.Deg2Rad;
        nextSpawnIndex++;
        return new Vector3(Mathf.Cos(angle) * 2f, 1f, Mathf.Sin(angle) * 2f);
    }

    [Server]
    public void BeginGame(int gameMode)
    {
        if (gameState == null) return;

        if (gameState.gameStatus != GameStatus.Lobby)
        {
            Debug.LogWarning("Cannot begin game - not in lobby!");
            return;
        }

        gameState.SetGameMode(gameMode);
        gameState.SetGameStatus(GameStatus.Ingame);

        // Load gameplay scene
        ServerChangeScene(GetSceneNameForGameMode(gameMode));
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (sceneName != "Main Menu" && gameState != null && gameState.gameStatus == GameStatus.Ingame)
        {
            SetupGameMode(gameState.selectedGameMode);
            MasterManager.Instance.Initialize();  // TODO: if i haven't finished, players must also have timers on their counterpart. server has them, clients don't.

            // Move all existing players to spawn positions
            int index = 0;
            foreach (var kvp in playerGameObjects)
            {
                if (kvp.Value != null)
                {
                    Vector3 spawnPos = GetSpawnPositionForIndex(index);
                    kvp.Value.transform.position = spawnPos;
                    ReenableYoShit(kvp.Value);
                    index++;
                }
            }
        }
    }

    private string GetSceneNameForGameMode(int gameMode)
    {
        switch (gameMode)
        {
            // TODO: oo[s
            case 0: // Endless
                return "GameplayScene";
            case 1: // Shifts
                return "GameplayScene";
            case 2: // Another mode
                return "GameplayScene";
            default:
                return "GameplayScene";
        }
    }

    [Server]
    private void SetupGameMode(int gameMode)
    {
        if (MasterManager.Instance != null)
        {
            GameObject timerMan = MasterManager.Instance.transform.Find("TimerManager").gameObject;
            if (timerMan != null)
            {
                foreach (Transform child in timerMan.transform)
                {
                    if (child != timerMan.transform)
                    {
                        Destroy(child.gameObject);
                    }
                }

                GameObject timerPrefab = null;
                switch (gameMode)
                {
                    case 0: // Endless
                        timerPrefab = endlessGamemodeTimerPrefab;
                        break;
                    case 1: // Shifts
                        timerPrefab = shiftsGamemodeTimerPrefab;
                        break;
                    case 2: // Another mode todo here ofc
                        timerPrefab = endlessGamemodeTimerPrefab;
                        break;
                }

                if (timerPrefab != null)
                {
                    GameObject timerInstance = Instantiate(timerPrefab, timerMan.transform);
                    timerInstance.transform.SetParent(timerMan.transform);
                    // Initialize timer
                    if (timerInstance.TryGetComponent<GenericTimer>(out var timer))
                    {
                        GameManager.Instance.timer = timer;
                        timer.Initialize(GameManager.Instance);
                        NetworkServer.Spawn(timerInstance);
                    }
                }
            }
        }
    }

    // absolute dogshit
    private void DisableYoShit(GameObject player)
    {
        player.GetComponent<PlayerController>().DisableYoShit(player);
    }

    private void ReenableYoShit(GameObject player)
    {
        player.GetComponent<PlayerController>().ReenableYoShit(player);
    }

    private Vector3 GetSpawnPositionForIndex(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[index % spawnPoints.Length].position;
        }

        // Fallback
        float angle = (index * 90f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * transform.position.x, transform.position.y, Mathf.Sin(angle) * transform.position.z);
    }

    // Public methods for UI
    public void StartHostGame(string port = "7777")
    {
        if (!string.IsNullOrEmpty(port))  // I have no idea how this can become suddenly null and how port will stay unset. I will have to forsee that at some later point, though. I can't be bothered making multiplayer working AND fixing all the bugs
        {
            var transport = Transport.active;

            if (transport is KcpTransport kcp)
            {
                if (ushort.TryParse(port, out var result))
                    if (result > 65535 || result < 1)
                    {
                        Debug.LogError("AFAIK computers have ports available only in range of [1, ... , 65535]. Sorry if that's not the case anymore.");
                        return;
                    }
                    else
                        kcp.Port = result;
                else
                {
                    Debug.LogError("Parsed port bad. Please dont put your credit card information there. Any number in [1, 2, ..., 65535] is fine, and if not the stall's just occupied probably.");
                    return;
                }
            }
            else
            {
                Debug.LogError("Somehow, no Transport found. Yell at coder plox, thbx.");
                return;
            }
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
                if (ushort.TryParse(split[1], out var result))
                    if (result > 65535 || result < 1)
                    {
                        Debug.LogError("AFAIK computers have ports available only in range of [1, ... , 65535]. Sorry if that's not the case anymore.");
                        return;
                    }
                    else
                        kcp.Port = result;
                else
                {
                    Debug.LogError("Parsed port bad. Please dont put your credit card information there. Any number in [1, 2, ..., 65535] is fine, and if not the stall's just occupied probably.");
                    return;
                }
            }
            else
            {
                Debug.LogError("Somehow, no Transport found. Yell at coder plox, thbx.");
                return;
            }
        }

        StartClient();
    }

    public void StopHostGame()
    {
        StopHost();
    }

    public void StopClientGame()
    {
        StopClient();
    }
}

using Mirror;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

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
            NetworkServer.Spawn(gameStateObj);
            gameStateObj.transform.SetParent(transform);
            gameState = gameStateObj.GetComponent<NetworkGameState>();
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
        player.SetActive(false);

        playerGameObjects[conn.connectionId] = player;

        if (gameState != null)
        {
            gameState.UpdatePlayerCount(numPlayers);
        }

        Debug.Log($"Player {conn.connectionId} spawned at {spawnPos}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (playerGameObjects.ContainsKey(conn.connectionId))
        {
            playerGameObjects.Remove(conn.connectionId);
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
            MasterManager.Instance.Initialize(); // make them all initialize again r smth idk
            // TODO: order manager doesn't seem to create the UI elements for the monitor dynamically i don't wanna know why atp but yeah

            // Move all existing players to spawn positions
            int index = 0;
            foreach (var kvp in playerGameObjects)
            {
                if (kvp.Value != null)
                {
                    Vector3 spawnPos = GetSpawnPositionForIndex(index);
                    kvp.Value.transform.position = spawnPos;
                    kvp.Value.SetActive(true);

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
                    }
                }
            }
        }
    }

    private Vector3 GetSpawnPositionForIndex(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[index % spawnPoints.Length].position;
        }

        // Fallback
        float angle = (index * 90f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * 2f, 1f, Mathf.Sin(angle) * 2f);
    }

    // Public methods for UI
    public void StartHostGame()
    {
        StartHost();
    }

    public void StartClientGame(string address = "localhost")
    {
        networkAddress = address;
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

public enum GameStatus
{
    Menu,   //  None, we're not preparing for the game
    Lobby,  //  We're preparing for a game
    Ingame  //  We're IN a game. Joining players must now join a new way (Unimplemented: box with the player in spawned ingame, player gets to see from the POV of the box. Players must use the box to unpack the player. If the box (with the player inside it) is destroyed, so is the player and they are not allowed to join the session.
}
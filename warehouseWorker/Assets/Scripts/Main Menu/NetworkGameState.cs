using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class NetworkGameState : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnGameStatusChanged))]
    public GameStatus gameStatus = GameStatus.Menu;

    [SyncVar(hook = nameof(OnGameModeChanged))]
    public int selectedGameMode = 0;

    [SyncVar]
    public int playerCount = 0;

    private NetworkGameManager networkManager;

    private void Start()
    {
        networkManager = NetworkGameManager.Instance;
    }

    private void OnGameStatusChanged(GameStatus oldStatus, GameStatus newStatus)
    {
        gameStatus = newStatus;
        Debug.Log($"Game status changed from {oldStatus} to {newStatus}");
    }

    private void OnGameModeChanged(int oldMode, int newMode)
    {
        selectedGameMode = newMode;
        Debug.Log($"Game mode changed to: {newMode}");
    }

    [Server]
    public void SetGameStatus(GameStatus status)
    {
        gameStatus = status;
    }

    [Server]
    public void SetGameMode(int mode)
    {
        selectedGameMode = mode;
    }

    [Server]
    public void UpdatePlayerCount(int count)
    {
        playerCount = count;
    }

    [ClientRpc]
    public void RpcInitializeAllManagers()
    {
        Debug.Log("Client received RpcInitializeAllManagers");

        // Clients initialize their local managers
        if (!isServer && MasterManager.Instance != null)
        {
            MasterManager.Instance.Initialize();
        }
    }

    [TargetRpc]
    public void TargetActivatePlayer(NetworkConnectionToClient target)
    {
        Debug.Log("Client activating player via TargetRpc");

        // This runs on the specific client's connection
        if (NetworkClient.localPlayer != null)
        {
            NetworkClient.localPlayer.gameObject.SetActive(true);

        }
    }

    [ClientRpc]
    public void RpcNotifyGameStart()
    {
        Debug.Log("Game is starting on client!");

        // Any client-side game start logic here
        // For example, disable main menu UI, show game UI, etc.
    }
}

public enum GameStatus
{
    Menu,   //  None, we're not preparing for the game
    Lobby,  //  We're preparing for a game
    Ingame  //  We're IN a game. Joining players must now join a new way (Unimplemented: box with the player in spawned ingame, player gets to see from the POV of the box. Players must use the box to unpack the player. If the box (with the player inside it) is destroyed, so is the player and they are not allowed to join the session.
}
using Mirror;
using UnityEngine;

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
}

using System;
using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// The ONLY NetworkBehaviour in the manager layer.
/// Owns every SyncVar, Command, and ClientRpc that the game needs.
/// All MonoBehaviour managers talk to this bridge instead of being NetworkBehaviours themselves.
///
/// Lifecycle:
///   1. NetworkGameManager creates the prefab and calls NetworkServer.Spawn().
///   2. Managers grab Instance during their Initialize() and subscribe to events.
/// </summary>
public class GameNetworkBridge : NetworkBehaviour
{
    #region Singleton

    public static GameNetworkBridge Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion

    #region Events (for MonoBehaviour managers to subscribe)

    /// <summary>Arg: new value of gameStarted</summary>
    public event Action<bool> GameStartedChanged;

    /// <summary>Arg: new score value</summary>
    public event Action<int> ScoreChanged;

    #endregion

    #region SyncVars — replicated game state

    [SyncVar(hook = nameof(OnGameStartedChanged))]
    public bool gameStarted;

    [SyncVar(hook = nameof(OnScoreChanged))]
    public int score;

    [SyncVar]
    public int levelSeed;

    #endregion

    #region SyncVar hooks (fire events for local subscribers)

    private void OnGameStartedChanged(bool _old, bool _new)
    {
        Debug.Log($"[Bridge] gameStarted {_old} → {_new}");
        GameStartedChanged?.Invoke(_new);
    }

    private void OnScoreChanged(int _old, int _new)
    {
        ScoreChanged?.Invoke(_new);
    }

    #endregion

    #region Score

    /// <summary>
    /// Call from anywhere. Server applies directly; client sends a Command.
    /// </summary>
    public void RequestAddScore(int amount, bool resetTimer, bool immediateReset)
    {
        if (isServer)
        {
            ApplyAddScore(amount, resetTimer, immediateReset);
        }
        else
        {
            CmdAddScore(amount, resetTimer, immediateReset);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdAddScore(int amount, bool resetTimer, bool immediateReset)
    {
        ApplyAddScore(amount, resetTimer, immediateReset);
    }

    /// <summary>Server-only: mutates the SyncVar and notifies GameManager.</summary>
    private void ApplyAddScore(int amount, bool resetTimer, bool immediateReset)
    {
        score += amount;

        // Let GameManager handle timer/order side-effects on the server.
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (immediateReset && gm.timer != null)
            gm.timer.ResetTimer();

        if (resetTimer)
        {
            if (gm.ordersManager != null)
                gm.ordersManager.GenerateNewOrderRequestee();
        }
    }

    #endregion

    #region Game flow — start / game over

    /// <summary>Server asks to begin the game.</summary>
    public void ServerStartGame()
    {
        if (!isServer) return;
        gameStarted = true;

        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.gameSave.score = score;
        try { gm.gameSaveData?.SaveData(gm.gameSave); }
        catch { Debug.LogWarning("[Bridge] Failed to save game data on start."); }

        if (gm.ordersManager != null && gm.items.Count > 0)
            gm.ordersManager.GenerateNewOrderRequestee();

        if (gm.timer != null)
            gm.timer.StartTimer();

        RpcStartGame();
    }

    [ClientRpc]
    private void RpcStartGame()
    {
        var gm = GameManager.Instance;
        if (gm?.timer != null)
            gm.timer.StartTimer();
    }

    public void ServerPauseGame()
    {
        if (!isServer) return;
        gameStarted = false;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.ordersManager != null)
            gm.ordersManager.ClearAllOrders();

        if (gm.timer != null)
            gm.timer.StopTimer();

        RpcPauseGame();
    }

    [ClientRpc]
    private void RpcPauseGame()
    {
        var gm = GameManager.Instance;
        if (gm?.timer != null) 
            gm.timer.StopTimer();
    }

    /// <summary>Request game over from any context.</summary>
    public void RequestGameOver()
    {
        if (isServer)
        {
            ServerGameOver();
        }
        else
        {
            CmdForceGameOver();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdForceGameOver()
    {
        ServerGameOver();
    }

    /// <summary>Server-only: tears everything down and notifies clients.</summary>
    private void ServerGameOver()
    {
        if (!isServer || !gameStarted) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Save cleanup
        try { gm.gameSaveData?.DeleteFile(); } catch { /* die silently */ }

        if (gm.timer != null)
            gm.timer.StopTimer();

        // End active events
        gm.ClearActiveEvents();

        gameStarted = false;
        RpcGameOver();
    }

    [ClientRpc]
    private void RpcGameOver()
    {
        // Kill all players
        var players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
            p.alive = false;

        // Leaderboard — server only
        if (isServer)
        {
            GameManager.Instance?.HandleLeaderboardOnServer();
        }

        // Game-over animation on local player
        var localPlayer = PlayerController.LocalPlayer;
        if (localPlayer != null)
        {
            if (localPlayer.TryGetComponent<Animator>(out var anim))
                anim.Play("GameOver");
        }
        else
        {
            Debug.Log("[Bridge] Couldn't find local player for game-over animation.");
        }

        GameManager.Instance?.StartCoroutine(ReturnToMainMenuAfterDelay(10f));
    }

    private IEnumerator ReturnToMainMenuAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (PlayerController.LocalPlayer != null)
        {
            PlayerController.LocalPlayer.Disconnect();
            Destroy(PlayerController.LocalPlayer.gameObject);
        }
        else
        {
            var any = FindAnyObjectByType<PlayerController>();
            if (any != null)
                Destroy(any.gameObject);
            else
                Debug.LogError("[Bridge] No player found at all during game-over cleanup.");
        }

        if (isServer)
        {
            NetworkServer.DisconnectAll();
            (NetworkGameManager.singleton ?? NetworkGameManager.Instance)?.StopHost();
        }
        NetworkClient.Disconnect();
        GameManager.Instance?.LoadSceneStr();
    }

    #endregion

    #region Orders — RPCs delegated from OrdersManager

    /// <summary>Plays an order-related sound on all clients.</summary>
    /// <param name="soundIndex">0=new, 1=complete, 2=fail</param>
    [ClientRpc]
    public void RpcPlayOrderSound(int soundIndex)
    {
        var om = OrdersManager.Instance;
        if (om == null) return;
        if (om.Source == null) return;

        switch (soundIndex)
        {
            case 0: om.Source.PlaySound(om.NewOrderClips); break;
            case 1: om.Source.PlaySound(om.OrderCompleteClips); break;
            case 2: om.Source.PlaySound(om.OrderFailClips); break;
        }
    }

    /// <summary>Updates a single requestee slot on all clients.</summary>
    [ClientRpc]
    public void RpcUpdateRequesteeSlot(int x, int y, float timeRemaining, float timeStart, bool exists)
    {
        OrdersManager.Instance?.UpdateRequesteeSlotUI(x, y, timeRemaining, timeStart, exists);
    }

    /// <summary>Updates a single order slot on all clients.</summary>
    [ClientRpc]
    public void RpcUpdateOrderSlot(int index, int orderType, int assignedBoxMaterial, bool exists)
    {
        OrdersManager.Instance?.UpdateOrderSlotUI(index, orderType, assignedBoxMaterial, exists);
    }

    /// <summary>Triggers a full UI refresh on all clients.</summary>
    [ClientRpc]
    public void RpcUpdateOrderUI()
    {
        OrdersManager.Instance?.UpdateOrderUI();
    }

    /// <summary>Syncs the entire orders snapshot to a newly-connected client.</summary>
    [ClientRpc]
    public void RpcSyncFullOrdersState()
    {
        OrdersManager.Instance?.SyncFullOrdersStateToPlayers();
    }

    #endregion

    #region Order delivery — Command delegated from OrdersManager

    /// <summary>Client asks server to process a delivery at a table.</summary>
    public void RequestProcessOrderDelivery(int table, uint itemNetId, bool fromShelf)
    {
        if (isServer)
        {
            OrdersManager.Instance?.ProcessOrderDeliveryServer(table, itemNetId, fromShelf);
        }
        else
        {
            CmdProcessOrderDelivery(table, itemNetId, fromShelf);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdProcessOrderDelivery(int table, uint itemNetId, bool fromShelf)
    {
        if (!NetworkServer.spawned.ContainsKey(itemNetId)) return;
        var itemObj = NetworkServer.spawned[itemNetId].gameObject;
        var item = itemObj.GetComponent<Item>();
        if (item == null) return;
        OrdersManager.Instance?.ProcessOrderDeliveryServer(table, item.netId, fromShelf);
    }

    #endregion

    #region Convenience — server helpers for MonoBehaviour managers

    /// <summary>Spawns a GameObject on the network (thin wrapper around NetworkServer.Spawn).</summary>
    [Server]
    public void ServerSpawn(GameObject obj)
    {
        if (obj == null) return;
        NetworkServer.Spawn(obj);
    }

    /// <summary>Spawns with owner.</summary>
    [Server]
    public void ServerSpawn(GameObject obj, GameObject owner)
    {
        if (obj == null || owner == null) return;
        NetworkServer.Spawn(obj, owner);
    }

    /// <summary>Destroys a networked object safely.</summary>
    [Server]
    public void ServerDestroy(GameObject obj)
    {
        if (obj == null) return;
        NetworkServer.Destroy(obj);
    }

    #endregion
}

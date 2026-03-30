using System.Reflection;
using UnityEngine;

/// <summary>
/// Orchestrator that bootstraps all managers in the correct order.
/// Now a pure MonoBehaviour — no networking attributes.
/// The <see cref="GameNetworkBridge"/> must already exist before this initializes.
/// </summary>
public class MasterManager : GenericManager<MasterManager>
{
    public GameObject masterManagerPrefab;

    // Manager references (assigned in Inspector or found at runtime)
    public GameManager GameManager;
    public AchievementManager AchievementManager;
    public IconManager IconManager;
    public MissionManager MissionManager;
    public LocalizationManager LocalizationManager;
    public OrdersManager OrdersManager;
    public ShelvesStockManager ShelvesStockManager;
    public ShopManager ShopManager;

    void Awake()
    {
        Initialize();
    }

    public override void Initialize()
    {
        base.Initialize();
        if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        // ---- Phase 1: bridge must already be spawned by NetworkGameManager ----
        if (GameNetworkBridge.Instance == null)
            Debug.LogError("[MasterManager] GameNetworkBridge.Instance is null! " +
                           "NetworkGameManager should have created & spawned it before we run.");

        // ---- Phase 2: non-game managers (no bridge dependency) ----
        LocalizationManager = FindManager(LocalizationManager);
        LocalizationManager.Initialize();

        AchievementManager = FindManager(AchievementManager);
        AchievementManager.Initialize();

        IconManager = FindManager(IconManager);
        IconManager.Initialize();

        MissionManager = FindManager(MissionManager);
        MissionManager.Initialize();

        ShopManager = FindManager(ShopManager);
        ShopManager.Initialize();

        // ---- Phase 3: game managers (depend on bridge) ----
        GameManager = FindManager(GameManager);
        OrdersManager = FindManager(OrdersManager);
        ShelvesStockManager = FindManager(ShelvesStockManager);
        ShelvesStockManager.Initialize(GameManager);

        // ---- Phase 4: GameManager last (it wires up bridge events, inits timers/orders) ----
        GameManager.Initialize();
    }

    #region Reflection helper

    private T FindManager<T>(T existing) where T : MonoBehaviour
    {
        if (existing != null) return existing;

        // Try singleton Instance property
        var prop = typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (prop != null && prop.PropertyType == typeof(T))
        {
            var inst = (T)prop.GetValue(null);
            if (inst != null) return inst;
        }

        // Try FindObjectOfType
        var found = FindObjectOfType<T>();
        if (found != null) return found;

        // Try children of GameManager
        if (GameManager.Instance != null)
        {
            found = GameManager.Instance.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        // Try masterManagerPrefab
        if (masterManagerPrefab != null)
        {
            var obj = Instantiate(masterManagerPrefab, transform);
            found = obj.GetComponent<T>();
            if (found == null) found = obj.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        // Last resort: create new GameObject with the component
        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(transform);
        return go.AddComponent<T>();
    }

    #endregion
}

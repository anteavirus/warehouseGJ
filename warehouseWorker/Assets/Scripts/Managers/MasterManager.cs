using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class MasterManager : GenericManager<MasterManager>
{
    // TODO: master manager, makes sure every single other manager is created, initialized and performs it's job in the order given
    public GameObject masterManagerPrefab;
    public GameManager GameManager;
    public AchievementManager AchievementManager;
    public IconManager IconManager;
    public MissionManager MissionManager;   
    public LocalizationManager LocalizationManager;
    public OrdersManager OrdersManager; 
    public ShelvesStockManager ShelvesStockManager; 
    public ShopManager ShopManager; 
    public WarehouseGeneratorManager WarehouseGeneratorManager;

    void Awake()
    {
        Initialize(); // RIGHT . RIGHT.  NOBODY IS FUCKING AWAKE.    
    }

    public override void Initialize()
    {
        base.Initialize();
        // SO . Fuck base.XXX() because it continues even if the base returns...
        if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
            DontDestroyOnLoad(gameObject); // we're keeping this mistake. 

        LocalizationManager = FindManager<LocalizationManager>(LocalizationManager);
        LocalizationManager.Initialize();

        // Try to find existing instances or create new ones
        AchievementManager = FindManager<AchievementManager>(AchievementManager);
        AchievementManager.Initialize();

        IconManager = FindManager<IconManager>(IconManager);
        IconManager.Initialize();

        GameManager = FindManager<GameManager>(GameManager);

        MissionManager = FindManager<MissionManager>(MissionManager);
        MissionManager.Initialize();

        OrdersManager = FindManager<OrdersManager>(OrdersManager);

        ShelvesStockManager = FindManager<ShelvesStockManager>(ShelvesStockManager);
        ShelvesStockManager.Initialize(GameManager);
        
        ShopManager = FindManager<ShopManager>(ShopManager);
        ShopManager.Initialize();

        WarehouseGeneratorManager = FindManager<WarehouseGeneratorManager>(WarehouseGeneratorManager);
        WarehouseGeneratorManager.Initialize();

        GameManager.Initialize();
    }

    private T FindManager<T>(T existingManager) where T : MonoBehaviour
    {
        // If already assigned, return it
        if (existingManager != null)
            return existingManager;

        // Try to get instance via singleton pattern if available
        T instance = null;

        // Check if the type has an Instance property (common singleton pattern)
        PropertyInfo instanceProperty = typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty != null && instanceProperty.PropertyType == typeof(T))
        {
            instance = (T)instanceProperty.GetValue(null);
        }

        // If not found via singleton, try to find in scene
        if (instance == null)
        {
            instance = FindObjectOfType<T>();
        }

        // If still not found, look in GameManager's children
        if (instance == null && GameManager.Instance != null)
        {
            instance = GameManager.Instance.GetComponentInChildren<T>(true);
        }

        // If still not found and we have a masterManagerPrefab, instantiate from prefab
        if (instance == null && masterManagerPrefab != null)
        {
            GameObject masterManagerInstance = Instantiate(masterManagerPrefab, transform);
            instance = masterManagerInstance.GetComponent<T>();

            // If the component is on the root of prefab but we need to search children
            if (instance == null)
            {
                instance = masterManagerInstance.GetComponentInChildren<T>(true);
            }
        }

        // If all else fails, create a new GameObject with the component
        if (instance == null)
        {
            GameObject managerObject = new GameObject(typeof(T).Name);
            managerObject.transform.SetParent(transform);
            instance = managerObject.AddComponent<T>();
        }

        return instance;
    }
}

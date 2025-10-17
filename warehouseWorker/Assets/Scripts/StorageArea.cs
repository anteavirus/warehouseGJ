using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StorageArea : MonoBehaviour
{
    [SerializeField] private List<int> _allowedItemIDs = new();
    public List<int> allowedItemIDs => _allowedItemIDs;

    Item containingObject;
    public int assignedItemID;
    public int itemAmount;
    BoxCollider boxCollider;
    [SerializeField] Vector3 limitVelocity = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("Shelf Properties")]
    public int shelfTypeID;

    [Header("Item Positioning")]
    [SerializeField] private Transform[] itemPositions;
    [SerializeField] private GameObject[] visualItems;

    [Header("Gizmo Settings")]
    [SerializeField] private Color availablePositionColor = Color.green;
    [SerializeField] private Color occupiedPositionColor = Color.red;
    [SerializeField] private float gizmoSphereSize = 0.1f;
    public Vector3 scaleOffset;

    // New: Flag to ensure initialization happens
    private bool isInitialized = false;

    // New: For delayed initialization
    private bool requiresVisualUpdate = false;

    void Start()
    {
        Initialize();
        isInitialized = true;

        // Process any delayed visual updates
        if (requiresVisualUpdate)
        {
            CreateVisualStock();
        }
    }

    public void Initialize()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        InitializeItemPositions();

        if (assignedItemID != 0)
        {
            containingObject = GameManager.Instance?.ReturnItemById(assignedItemID);
            if (containingObject != null)
            {
                containingObject.fromShelf = true;
            }
        }
    }

    private void InitializeItemPositions()
    {
        // CRITICAL FIX #2: Always recreate both arrays together
        List<Transform> positions = new List<Transform>();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("ItemPosition") || child.CompareTag("ItemPosition"))
            {
                positions.Add(child);
            }
        }

        itemPositions = positions.ToArray();
        // Ensure BOTH arrays are the same length
        visualItems = new GameObject[itemPositions.Length];

        // Clear all positions initially
        for (int i = 0; i < visualItems.Length; i++)
        {
            visualItems[i] = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {   
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) 
            return;

        if (!other.TryGetComponent<Item>(out var item)) 
            return;   

        Vector3 velocity = rb.velocity;
        bool isVelocityUnderLimit =
            Mathf.Abs(velocity.x) < limitVelocity.x &&
            Mathf.Abs(velocity.y) < limitVelocity.y &&
            Mathf.Abs(velocity.z) < limitVelocity.z;

        if (isVelocityUnderLimit)
        {
            if (item.isActiveAndEnabled && IsItemAllowed(item.ID))
            {
                if (GameManager.Instance != null && !item.fromShelf)
                {
                    GameManager.Instance.AddScore(item.scoreValue);
                    GameManager.Instance.setdownItem = true;
                }
                AddItemToShelf();
                item.enabled = false;
                Destroy(other.gameObject);
            }
        }
    }

    private bool IsItemAllowed(int itemID)
    {
        //if (_allowedItemIDs.Count == 0) return true; // todo kms
        return _allowedItemIDs.Contains(itemID) || assignedItemID == itemID;
    }

    private void AddItemToShelf()
    {
        if (itemAmount >= GetMaxCapacity())
            return; // Prevent overflow

        itemAmount++;

        // Find an empty position and place a visual item there
        int emptyPosition = FindEmptyPosition();
        if (emptyPosition != -1)
        {
            CreateSingleVisualItem(emptyPosition);
        }
    }

    public GameObject CreateNewItemForPickup()
    {
        if (containingObject == null || itemAmount <= 0) return null;

        GameObject obj = containingObject.gameObject;
        GameObject created = Instantiate(obj);
        created.name = containingObject.name;
        created.SetActive(true);

        if (created.TryGetComponent<Item>(out var createdItem))
        {
            createdItem.fromShelf = true;
            createdItem.enabled = true;
        }

        RemoveItemFromShelf();
        return created;
    }

    public void CreateVisualStock()
    {
        // CRITICAL FIX #3: Check both initialization AND nulls
        if (!isInitialized)
        {
            requiresVisualUpdate = true;
            return;
        }

        InitializeItemPositions();

        if (containingObject == null || itemAmount <= 0 || itemPositions == null || itemPositions.Length == 0)
            return;
        

        ClearAllVisualItems();

        // Use visualItems.Length instead of itemPositions.Length for safety
        int itemsToCreate = Mathf.Min(itemAmount, visualItems.Length);

        for (int i = 0; i < itemsToCreate; i++)
        {
            CreateSingleVisualItem(i);
        }
    }

    private void CreateSingleVisualItem(int positionIndex)
    {
        // CRITICAL FIX #4: Check against visualItems.Length - NOT itemPositions.Length
        if (positionIndex < 0 || positionIndex >= visualItems.Length)
        {
            Debug.LogWarning($"[StorageArea] Position index {positionIndex} out of bounds! Max: {visualItems.Length}");
            return;
        }

        // First check if we already have a visual item
        if (visualItems[positionIndex] != null) return;

        // THEN check item positions (which should be the same size)
        if (positionIndex >= itemPositions.Length || itemPositions[positionIndex] == null)
            return;

        if (itemPositions[positionIndex].childCount > 0)
            return;

        if (containingObject == null)
        {
            Debug.LogWarning($"[StorageArea] No containing object for {name} when trying to create visual item");
            return;
        }

        GameObject visualItem = Instantiate(containingObject.gameObject, itemPositions[positionIndex]);
        visualItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        visualItem.transform.localScale = UsefulStuffs.Divide(containingObject.transform.localScale, scaleOffset); // TODO: this doesn't look right. fuck it up
        visualItem.SetActive(true);
        visualItem.transform.parent.gameObject.SetActive(true);

        if (visualItem.TryGetComponent<Item>(out var itemComponent))
            Destroy(itemComponent);

        if (visualItem.TryGetComponent<Rigidbody>(out var rb))
            Destroy(rb);

        visualItem.name = $"Visual_{containingObject.name}_{positionIndex}";

        visualItems[positionIndex] = visualItem;
    }

    private void ClearAllVisualItems()
    {
        for (int i = 0; i < visualItems?.Length; i++)
        {
            if (visualItems[i] != null)
            {
                Destroy(visualItems[i]);
                visualItems[i] = null;
            }
        }
    }

    private bool IsPositionOccupied(int positionIndex)
    {
        if (positionIndex < 0 || positionIndex >= itemPositions.Length ||
            itemPositions[positionIndex] == null)
            return false;

        // Check if we have a tracked visual item
        if (positionIndex < visualItems.Length && visualItems[positionIndex] != null)
            return true;

        // Fallback: check hierarchy directly
        return itemPositions[positionIndex].childCount > 0;
    }

    private int FindOccupiedPosition()
    {
        // Start from the BACK (most recently added items)
        for (int i = itemPositions.Length - 1; i >= 0; i--)
        {
            if (IsPositionOccupied(i))
                return i;
        }
        return -1; // No occupied positions available
    }

    private int FindEmptyPosition()
    {
        for (int i = 0; i < itemPositions.Length; i++)
        {
            if (!IsPositionOccupied(i))
                return i;
        }
        return -1; // No empty positions available
    }

    private void RemoveItemFromShelf()
    {
        itemAmount = Mathf.Max(0, itemAmount - 1);

        int occupiedPosition = FindOccupiedPosition();

        // ADD SAFETY CHECK: Only remove if we found a position
        if (occupiedPosition != -1)
        {
            Debug.Log($"Removing item from position {occupiedPosition}. New count: {itemAmount}");
            RemoveVisualItem(occupiedPosition);

            // DOUBLE SAFETY: Verify counts match after removal
            int visualCount = visualItems?.Count(v => v != null) ?? 0;
            if (visualCount != itemAmount)
            {
                Debug.LogWarning($"[StorageArea] Visual count ({visualCount}) doesn't match logical count ({itemAmount})");
                SyncVisualsWithLogic();
            }
        }
        else if (itemAmount > 0)
        {
            Debug.LogError($"[StorageArea] Item count says {itemAmount} items, but no visual items found!");
            SyncVisualsWithLogic();
        }
    }

    private void SyncVisualsWithLogic()
    {
        ClearAllVisualItems();

        int itemsToShow = Mathf.Min(itemAmount, itemPositions?.Length ?? 0);
        for (int i = 0; i < itemsToShow; i++)
        {
            if (i < visualItems.Length)
            {
                CreateSingleVisualItem(i);
            }
        }
    }

    private void RemoveVisualItem(int positionIndex)
    {
        if (positionIndex < 0 || positionIndex >= visualItems.Length) return;

        // Stop tracking the visual item first
        if (visualItems[positionIndex] != null)
        {
            Destroy(visualItems[positionIndex]);
            visualItems[positionIndex] = null;
        }

        // Safety counter prevents infinite loop
        int safetyCount = 0;
        int maxAttempts = itemPositions[positionIndex]?.childCount ?? 0;

        // Only attempt to destroy what we know exists
        while (safetyCount < maxAttempts &&
               positionIndex < itemPositions.Length &&
               itemPositions[positionIndex] != null &&
               itemPositions[positionIndex].childCount > 0)
        {
            Transform child = itemPositions[positionIndex].GetChild(0);
            if (child != null && child.gameObject != null)
            {
                Destroy(child.gameObject);
            }
            safetyCount++;
        }
    }

    public int GetAvailablePositions()
    {
        if (itemPositions == null)
            InitializeItemPositions();

        int count = 0;
        for (int i = 0; i < itemPositions?.Length; i++)
        {
            if (itemPositions[i] == null) continue;
            if (itemPositions[i].childCount == 0)
                count++;
        }
        return count;
    }

    public int GetMaxCapacity()
    {
        if (itemPositions == null)
            InitializeItemPositions();

        return itemPositions?.Length ?? 0;
    }

    private void OnDrawGizmos()
    {
        // Fix #5: Better handling for scene view
        bool isPlaying = Application.isPlaying;

        // Ensure we have item positions even in editor
        if ((itemPositions == null || itemPositions.Length == 0) && !isPlaying)
        {
            List<Transform> positions = new List<Transform>();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("ItemPosition"))
                {
                    positions.Add(child);
                }
            }

            itemPositions = positions.ToArray();
        }

        // Draw gizmos for each item position
        if (itemPositions != null)
        {
            for (int i = 0; i < itemPositions.Length; i++)
            {
                if (itemPositions[i] == null) continue;

                Vector3 position = itemPositions[i].position;

                // Choose color based on position state
                Color gizmoColor;
                if (itemPositions[i].childCount > 0)
                {
                    gizmoColor = occupiedPositionColor;
                }
                else
                {
                    gizmoColor = availablePositionColor;
                }

                Gizmos.color = gizmoColor;
                Gizmos.DrawSphere(position, gizmoSphereSize);

                // Draw a small cube to show orientation
                Gizmos.color = Color.white;
                Gizmos.matrix = Matrix4x4.TRS(position, itemPositions[i].rotation, Vector3.one * 0.05f);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        // Draw shelf bounds
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();

        if (boxCollider != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

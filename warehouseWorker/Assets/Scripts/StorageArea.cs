using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Color noItemPositionColor = Color.yellow;
    [SerializeField] private float gizmoSphereSize = 0.1f;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        InitializeItemPositions();

        if (assignedItemID != 0)
        {
            containingObject = GameManager.Instance.ReturnItemById(assignedItemID);
            if (containingObject != null)
            {
                containingObject.fromShelf = true;
            }
        }
    }

    private void InitializeItemPositions()
    {
        // Find all child transforms that are designated as item positions
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
        visualItems = new GameObject[itemPositions.Length];

        // Clear all positions initially
        for (int i = 0; i < itemPositions.Length; i++)
        {
            visualItems[i] = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        Vector3 velocity = rb.velocity;
        bool isVelocityUnderLimit =
            Mathf.Abs(velocity.x) < limitVelocity.x &&
            Mathf.Abs(velocity.y) < limitVelocity.y &&
            Mathf.Abs(velocity.z) < limitVelocity.z;

        if (isVelocityUnderLimit)
        {
            Item item = other.GetComponent<Item>();
            if (item != null && item.isActiveAndEnabled && IsItemAllowed(item.ID))
            {
                if (GameManager.Instance != null && !item.fromShelf)
                {
                    GameManager.Instance.AddScore(item.scoreValue);
                    GameManager.Instance.setdownItem = true;
                    AddItemToShelf();
                }
                item.enabled = false;
                Destroy(other.gameObject);
            }
        }
    }

    private bool IsItemAllowed(int itemID)
    {
        if (_allowedItemIDs.Count == 0) return true;
        return _allowedItemIDs.Contains(itemID) || assignedItemID == itemID;
    }

    private void AddItemToShelf()
    {
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

        var obj = containingObject.gameObject;
        var created = Instantiate(obj);
        created.name = containingObject.name;
        created.SetActive(true);

        if (created.TryGetComponent<Item>(out var createdItem))
        {
            createdItem.fromShelf = false;
            createdItem.enabled = true;
        }

        RemoveItemFromShelf();
        return created;
    }

    private void RemoveItemFromShelf()
    {
        itemAmount--;

        // Find an occupied position and remove the visual item
        int occupiedPosition = FindOccupiedPosition();
        if (occupiedPosition != -1)
        {
            RemoveVisualItem(occupiedPosition);
        }
    }

    public void CreateVisualStock()
    {
        if (containingObject == null || itemAmount <= 0) return;

        // Clear existing visual items
        ClearAllVisualItems();

        // Create visual items up to the available positions or itemAmount, whichever is smaller
        int itemsToCreate = Mathf.Min(itemAmount, itemPositions.Length);

        for (int i = 0; i < itemsToCreate; i++)
        {
            CreateSingleVisualItem(i);
        }
    }

    private void CreateSingleVisualItem(int positionIndex)
    {
        if (positionIndex < 0 || positionIndex >= itemPositions.Length) return;
        if (itemPositions[positionIndex].GetChild(0)) return;
        if (containingObject == null) return;

        GameObject visualItem = Instantiate(containingObject.gameObject);

        // Remove Item component to make it non-pickupable
        if (visualItem.TryGetComponent<Item>(out var itemComponent))
            Destroy(itemComponent);
        
        if (visualItem.TryGetComponent<Rigidbody>(out var rb)) 
            Destroy(rb);

        // Position the item at the designated position
        visualItem.transform.SetPositionAndRotation(itemPositions[positionIndex].position, 
                                                    itemPositions[positionIndex].rotation);
        visualItem.transform.SetParent(itemPositions[positionIndex]);

        visualItem.name = $"Visual_{containingObject.name}_{positionIndex}";

        visualItems[positionIndex] = visualItem;
    }

    private void RemoveVisualItem(int positionIndex)
    {
        if (positionIndex < 0 || positionIndex >= itemPositions.Length) return;
        if (!itemPositions[positionIndex]?.GetChild(0)) return;

        if (visualItems[positionIndex] != null)
        {
            Destroy(visualItems[positionIndex]);
            visualItems[positionIndex] = null;
        }
    }

    private void ClearAllVisualItems()
    {
        for (int i = 0; i < visualItems.Length; i++)
        {
            if (visualItems[i] != null)
            {
                Destroy(visualItems[i]);
                visualItems[i] = null;
            }
        }
    }

    private int FindEmptyPosition()
    {
        for (int i = 0; i < itemPositions.Length; i++)
        {
            if (itemPositions[i].childCount <= 0) continue;
            if (!itemPositions[i]?.GetChild(0))
                return i;
        }
        return -1; // No empty positions available
    }

    private int FindOccupiedPosition()
    {
        for (int i = itemPositions.Length - 1; i >= 0; i--)
        {
            if (itemPositions[i].childCount <= 0) continue;
            if (itemPositions[i]?.GetChild(0))
                return i;
        }
        return -1; // No occupied positions available
    }

    public int GetAvailablePositions()
    {
        int count = 0;
        for (int i = 0; i < itemPositions.Length; i++)
        {
            if (itemPositions[i].childCount <= 0) continue;
            if (!itemPositions[i]?.GetChild(0))
                count++;
        }
        return count;
    }

    public int GetMaxCapacity()
    {
        return itemPositions.Length;
    }

    private void OnDrawGizmos()
    {
        if (itemPositions == null || itemPositions.Length == 0)
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
        for (int i = 0; i < itemPositions.Length; i++)
        {
            if (itemPositions[i] == null) continue;

            Vector3 position = itemPositions[i].position;

            // Choose color based on position state
            Color gizmoColor;
            if (Application.isPlaying)
            {
                if (itemPositions[i] != null && itemPositions[i].childCount > 0 && i < itemPositions.Length)
                {
                    gizmoColor = itemPositions[i].GetChild(0) != null ? occupiedPositionColor : availablePositionColor;
                }
                else
                {
                    gizmoColor = noItemPositionColor;
                }
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

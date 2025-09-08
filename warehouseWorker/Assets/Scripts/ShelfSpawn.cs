using System;
using UnityEngine;

public class ShelfSpawn : MonoBehaviour
{
    [Header("Spawn Restrictions")]
    [Tooltip("Leave empty to accept all shelf types")]
    public int[] acceptedShelfTypes;

    // These fields track which item/shelf has been assigned to this spawn
    [NonSerialized]
    public int assignedItemID = 0; // 0 means no item
    [NonSerialized]
    public StorageArea assignedShelfPrefab = null;
    [NonSerialized]
    public int assignedItemAmount = 0;

    public bool CanAcceptShelfType(int shelfTypeID)
    {
        if (acceptedShelfTypes == null || acceptedShelfTypes.Length == 0)
            return true; // Accept all if no restrictions

        foreach (int acceptedType in acceptedShelfTypes)
        {
            if (acceptedType == shelfTypeID)
                return true;
        }
        return false;
    }

    public bool IsActiveForAssignment()
    {
        return gameObject.activeInHierarchy && enabled;
    }

    public bool IsAssigned()
    {
        return assignedShelfPrefab != null && assignedItemID != 0;
    }

    public void AssignItemAndShelf(int ID, StorageArea chosenShelf, int amount = 0)
    {
        assignedItemID = ID;
        assignedShelfPrefab = chosenShelf;
        assignedItemAmount = amount;
    }

    public void ResetAssignment()
    {
        assignedItemID = 0;
        assignedShelfPrefab = null;
        assignedItemAmount = 0;
    }

    private void OnDrawGizmos()
    {
        Color color = gameObject.activeInHierarchy ? Color.green : Color.red;

        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        Gizmos.color = color * 0.7f;
        Gizmos.DrawSphere(transform.position, 0.1f);

        float axisLength = 0.5f;

        Vector3 xDirection = transform.rotation * Vector3.right;
        Vector3 yDirection = transform.rotation * Vector3.up;
        Vector3 zDirection = transform.rotation * Vector3.forward;

        Vector3 origin = transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + xDirection * axisLength);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + yDirection * axisLength);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + zDirection * axisLength);
    }
}

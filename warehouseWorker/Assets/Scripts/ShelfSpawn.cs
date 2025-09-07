using UnityEngine;

public class ShelfSpawn : MonoBehaviour
{
    [Header("Spawn Restrictions")]
    [Tooltip("Leave empty to accept all shelf types")]
    public int[] acceptedShelfTypes;

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

    private void OnDrawGizmos()
    {
        // Visual indicator in scene view
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.1f);
    }
}

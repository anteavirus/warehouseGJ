using Mirror;
using System;
using UnityEngine;

public class ShelfSpawn : NetworkBehaviour
{
    [Header("Spawn Restrictions")]
    [Tooltip("Leave empty to accept all shelf types")]
    public int[] acceptedShelfTypes;
    readonly bool stopEatingMyFPSWhenLookedViewedGizmos = false;
    ShelvesStockManager shelfManager;

    public StorageArea assignedShelfPrefab = null;

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
        return assignedShelfPrefab != null;
    }

    // todo: clear up methods and. just everything cuz. shit is kinda outdated
    public void AssignItemAndShelf(int ID, StorageArea chosenShelf, int amount = 0)
    {
        assignedShelfPrefab = chosenShelf;
    }

    public void ResetAssignment()
    {
        assignedShelfPrefab = null;
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

#if UNITY_EDITOR
        if (stopEatingMyFPSWhenLookedViewedGizmos) return;
        // TODO: this might be laggy. piss on thisa

        assignedShelfPrefab = null;
        if (shelfManager == null)
            shelfManager = FindObjectOfType<ShelvesStockManager>();
        var shelfPrefabs = shelfManager.shelfPrefabs;

        if (shelfPrefabs?.Count <= 0)
            shelfManager.UpdateShelfStoragesFromPrefabs();

        if (shelfPrefabs?.Count > 0)
        {
            foreach (var item in shelfPrefabs)
            {
                var a = UsefulStuffs.FindComponentInChildren<StorageArea>(item);
                if (a != null)
                {
                    foreach (var b in acceptedShelfTypes)
                    {
                        if (a.allowedItemIDs.Contains(b))
                        {
                            assignedShelfPrefab = a;
                        }
                    if (assignedShelfPrefab != null) break;
                    }
                }
            }
        }

        if (assignedShelfPrefab != null)
        {
            Gizmos.color = new Color(1,1,1,0.05f);
            var meshShit = assignedShelfPrefab.transform.parent.GetComponent<MeshFilter>();
            Gizmos.DrawWireMesh(meshShit.sharedMesh, 0, transform.position, transform.rotation, UsefulStuffs.Multiply(Vector3.one,meshShit.transform.localScale));
        }
#else
        var definitelyNotDataMinedString = "Hi! If this were to be a development build, you'd see a lot more of disgusting shit where I hackily render every single shelf mesh on screen. Every single frame. Created and destroyed. Just to show that. Pretty cool, right? Anyway I'm sure you don't wanna see *that*, right? >:3 oh btw yeah that's what that bool is for (i.e. 'fuck off i dont wanna see ts')";
#endif
    }
}

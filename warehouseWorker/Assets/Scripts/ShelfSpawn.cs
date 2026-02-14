using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShelfSpawn : MonoBehaviour
{
    [Header("Spawn Restrictions")]
    [Tooltip("Leave empty to accept all shelf types")]
    public int[] acceptedShelfTypes;

    [Header("Override")]
    [Tooltip("If assigned, this specific shelf prefab will be used regardless of restrictions (useful for manual placement)")]
    public StorageArea assignedShelfPrefab = null;

    [Header("Editor Preview")]
    [Tooltip("If true, draws all accepted shelf prefabs as a wireframe grid around the spawn point (editor only)")]
    public bool showAllAcceptedShelves = false;

    [Tooltip("When true, skips the heavy gizmo drawing (useful if the editor becomes laggy)")]
    public bool stopEatingMyFPSWhenLookedViewedGizmos = false;

    private ShelvesStockManager shelfManager;
    private List<GameObject> cachedShelfPrefabs; // cache to avoid repeated lookups

    // ----------------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------------

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

    /// <summary>
    /// Assign a specific shelf prefab to this spawn point.
    /// </summary>
    public void AssignItemAndShelf(int ID, StorageArea chosenShelf, int amount = 0)
    {
        assignedShelfPrefab = chosenShelf;
        // Note: ID and amount are ignored; kept for compatibility.
    }

    public void ResetAssignment()
    {
        assignedShelfPrefab = null;
    }

    // ----------------------------------------------------------------------------
    // Editor Gizmos
    // ----------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        // Always draw the basic transform gizmo
        DrawTransformGizmo();

        if (stopEatingMyFPSWhenLookedViewedGizmos)
            return;

        // Cache manager and prefab list
        if (shelfManager == null)
            shelfManager = FindObjectOfType<ShelvesStockManager>();
        if (shelfManager == null)
            return;

        if (cachedShelfPrefabs == null || cachedShelfPrefabs.Count == 0)
        {
            shelfManager.UpdateShelfStoragesFromPrefabs(); // ensure list is built
            cachedShelfPrefabs = shelfManager.shelfPrefabs;
        }

        // 1. Draw the overridden shelf if any
        if (assignedShelfPrefab != null)
        {
            DrawShelfMesh(assignedShelfPrefab, Color.white * new Color(1,1,1,0.1f));
        }

        // 2. If requested, draw all accepted shelves in a grid
        if (showAllAcceptedShelves && cachedShelfPrefabs != null)
        {
            DrawAllAcceptedShelvesGrid();
        }
    }

    private void DrawTransformGizmo()
    {
        // Base cube and sphere
        Color color = gameObject.activeInHierarchy ? Color.green : Color.red;
        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        Gizmos.color = color * 0.7f;
        Gizmos.DrawSphere(transform.position, 0.1f);

        // Axes
        float axisLength = 0.5f;
        Vector3 origin = transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + transform.right * axisLength);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + transform.up * axisLength);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + transform.forward * axisLength);
    }

    private void DrawShelfMesh(StorageArea shelf, Color color)
    {
        if (shelf == null) return;

        Transform parent = shelf.transform.parent;
        if (parent == null) return;

        MeshFilter meshFilter = parent.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Gizmos.color = color;
        // Use the parent's lossy scale for world size
        Vector3 worldScale = parent.lossyScale;
        Gizmos.DrawWireMesh(meshFilter.sharedMesh, 0, transform.position, transform.rotation, worldScale);
    }

    private void DrawAllAcceptedShelvesGrid()
    {
        if (acceptedShelfTypes == null || acceptedShelfTypes.Length == 0)
            return; // nothing to show

        int index = 0;
        float spacing = 2f; // distance between previews
        int columns = 3;

        foreach (GameObject prefab in cachedShelfPrefabs)
        {
            if (prefab == null) continue;

            StorageArea area = prefab.GetComponentInChildren<StorageArea>();
            if (area == null) continue;

            // Check if this shelf's allowed IDs intersect with any accepted type
            bool matches = false;
            foreach (int allowedId in area.allowedItemIDs)
            {
                if (System.Array.IndexOf(acceptedShelfTypes, allowedId) >= 0)
                {
                    matches = true;
                    break;
                }
            }
            if (!matches) continue;

            // Position in a grid around the spawn point
            int row = index / columns;
            int col = index % columns;
            Vector3 offset = new Vector3((col - columns / 2f) * spacing, 0, row * spacing);
            Vector3 previewPos = transform.position + offset;

            // Save current gizmo matrix to draw at previewPos
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(previewPos, transform.rotation, Vector3.one);

            // Draw a semi‑transparent mesh
            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Gizmos.color = new Color(1, 1, 1, 0.3f);
                Gizmos.DrawWireMesh(meshFilter.sharedMesh, -prefab.transform.position); // adjust because matrix already sets position
            }

            Gizmos.matrix = originalMatrix;
            index++;
        }
    }
}
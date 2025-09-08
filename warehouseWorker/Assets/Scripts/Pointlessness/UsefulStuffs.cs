using UnityEngine;

public static class UsefulStuffs
{
    // --- Vector3 Math Operations ---
    public static Vector3 Divide(this Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.x / b.x,
            a.y / b.y,
            a.z / b.z
        );
    }

    public static void SetLayerRecursively(this GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            child.gameObject.SetLayerRecursively(layer);
        }
    }

    public static bool IsOnLayer(GameObject obj, string layerName)
    {
        return obj.layer == LayerMask.NameToLayer(layerName);
    }

    public static Vector3 Divide(this Vector3 a, float b)
    {
        return new Vector3(a.x / b, a.y / b, a.z / b);
    }

    public static Vector3 Multiply(this Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.x * b.x,
            a.y * b.y,
            a.z * b.z
        );
    }

    // --- Vector2 Math Operations ---
    public static Vector2 Divide(this Vector2 a, Vector2 b)
    {
        return new Vector2(
            a.x / b.x,
            a.y / b.y
        );
    }

    public static Vector2 Divide(this Vector2 a, float b)
    {
        return new Vector2(a.x / b, a.y / b);
    }

    public static Vector2 Multiply(this Vector2 a, Vector2 b)
    {
        return new Vector2(
            a.x * b.x,
            a.y * b.y
        );
    }

    // --- Clamp Each Component ---
    public static Vector3 Clamp(Vector3 v, Vector3 min, Vector3 max)
    {
        return new Vector3(
            Mathf.Clamp(v.x, min.x, max.x),
            Mathf.Clamp(v.y, min.y, max.y),
            Mathf.Clamp(v.z, min.z, max.z)
        );
    }

    public static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max)
    {
        return new Vector2(
            Mathf.Clamp(v.x, min.x, max.x),
            Mathf.Clamp(v.y, min.y, max.y)
        );
    }

    // --- Lerp Each Component Individually (Not always built-in) ---
    public static Vector3 Lerp(Vector3 a, Vector3 b, Vector3 t)
    {
        return new Vector3(
            Mathf.Lerp(a.x, b.x, t.x),
            Mathf.Lerp(a.y, b.y, t.y),
            Mathf.Lerp(a.z, b.z, t.z)
        );
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, Vector2 t)
    {
        return new Vector2(
            Mathf.Lerp(a.x, b.x, t.x),
            Mathf.Lerp(a.y, b.y, t.y)
        );
    }

    // --- Check Near Zero ---
    public static bool IsNearZero(this Vector3 v, float epsilon = 1e-5f)
    {
        return v.sqrMagnitude < epsilon * epsilon;
    }

    public static bool IsNearZero(this Vector2 v, float epsilon = 1e-5f)
    {
        return v.sqrMagnitude < epsilon * epsilon;
    }

    // --- Inverse Each Component (useful for scaling) ---
    public static Vector3 Inverse(this Vector3 v)
    {
        return new Vector3(
            v.x != 0 ? 1f / v.x : 0f,
            v.y != 0 ? 1f / v.y : 0f,
            v.z != 0 ? 1f / v.z : 0f
        );
    }

    public static Vector2 Inverse(this Vector2 v)
    {
        return new Vector2(
            v.x != 0 ? 1f / v.x : 0f,
            v.y != 0 ? 1f / v.y : 0f
        );
    }
}

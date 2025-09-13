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

    // Add these to your UsefulStuffs class if needed:

    /// <summary>
    /// Linearly interpolates between two colors
    /// </summary>
    public static Color LerpColor(Color a, Color b, float t)
    {
        return Color.Lerp(a, b, Mathf.Clamp01(t));
    }

    /// <summary>
    /// Creates a color with modified alpha
    /// </summary>
    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }

    /// <summary>
    /// Creates a Color from a hex string (with or without #)
    /// </summary>
    public static Color ColorFromHex(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length == 6)
        {
            hex += "FF";
        }
        else if (hex.Length == 3)
        {
            hex = string.Format("{0}{0}{1}{1}{2}{2}FF", hex[0], hex[1], hex[2]);
        }
        else if (hex.Length != 8)
        {
            Debug.LogWarning($"Invalid hex format: {hex}. Expected 3, 6, or 8 characters.");
            return Color.white;
        }

        try
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return new Color32(r, g, b, a);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to parse hex color: {hex}. Error: {e.Message}");
            return Color.white;
        }
    }

    /// <summary>
    /// Adjusts brightness of a color (multiplies RGB values)
    /// </summary>
    public static Color AdjustBrightness(Color color, float brightnessMultiplier)
    {
        brightnessMultiplier = Mathf.Clamp(brightnessMultiplier, 0f, 2f);
        return new Color(
            color.r * brightnessMultiplier,
            color.g * brightnessMultiplier,
            color.b * brightnessMultiplier,
            color.a
        );
    }

    /// <summary>
    /// Converts Color to hex string (with # prefix)
    /// </summary>
    public static string ColorToHex(Color color)
    {
        Color32 color32 = color;
        return $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
    }

    /// <summary>
    /// Generates a random color
    /// </summary>
    public static Color RandomColor()
    {
        return new Color(
            UnityEngine.Random.value,
            UnityEngine.Random.value,
            UnityEngine.Random.value,
            1f
        );
    }

    /// <summary>
    /// Calculates the luminance (perceived brightness) of a color
    /// </summary>
    public static float CalculateLuminance(Color color)
    {
        return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
    }

    /// <summary>
    /// Returns black or white text color based on background color for optimal contrast
    /// </summary>
    public static Color GetContrastColor(Color backgroundColor)
    {
        return CalculateLuminance(backgroundColor) > 0.5f ? Color.black : Color.white;
    }

    /// <summary>
    /// Creates a color from HSV values (Hue: 0-360, Saturation: 0-1, Value: 0-1)
    /// </summary>
    public static Color ColorFromHSV(float hue, float saturation, float value)
    {
        hue = Mathf.Repeat(hue, 360f);
        saturation = Mathf.Clamp01(saturation);
        value = Mathf.Clamp01(value);

        float chroma = value * saturation;
        float huePrime = hue / 60f;
        float x = chroma * (1 - Mathf.Abs(huePrime % 2 - 1));

        Color color = Color.black;

        if (huePrime >= 0 && huePrime < 1) color = new Color(chroma, x, 0);
        else if (huePrime >= 1 && huePrime < 2) color = new Color(x, chroma, 0);
        else if (huePrime >= 2 && huePrime < 3) color = new Color(0, chroma, x);
        else if (huePrime >= 3 && huePrime < 4) color = new Color(0, x, chroma);
        else if (huePrime >= 4 && huePrime < 5) color = new Color(x, 0, chroma);
        else if (huePrime >= 5 && huePrime < 6) color = new Color(chroma, 0, x);

        float m = value - chroma;
        return new Color(color.r + m, color.g + m, color.b + m);
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

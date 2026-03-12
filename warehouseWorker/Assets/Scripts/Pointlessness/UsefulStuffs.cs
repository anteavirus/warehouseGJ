using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
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

    public static bool ContainsLayer(this LayerMask mask, int layer)
    {
        return mask == (mask | (1 << layer));
    }

    public static bool IsTrulyNull(this object obj)
    {
        return obj == null || (UnityEngine.Object) obj == null;
    }

    /// <summary>
    /// Converts a time string to seconds since midnight
    /// Supported formats:
    /// - 24-hour: "14:30", "14:30:45", "1430"
    /// - 12-hour: "2:30 PM", "2:30:15 PM", "02:30am", "2pm"
    /// - Simple: "14.5" (hours.decimal), "14.5h" 
    /// - Relative: "+2h30m", "+2:30"
    /// </summary>
    /// <param name="timeString">The time string to parse</param>
    /// <returns>Seconds since midnight (0 - 86399)</returns>
    /// <exception cref="ArgumentException">Thrown when time string is invalid</exception>
    public static long TimeStringToSeconds(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
        {
            throw new ArgumentException("Time string cannot be null or empty");
        }

        string input = timeString.Trim().ToUpper();

        try
        {
            // 1. Handle relative times (e.g., "+2h30m", "+1:30")
            if (input.StartsWith("+"))
            {
                return ParseRelativeTime(input.Substring(1));
            }

            // 2. Handle decimal hours (e.g., "14.5", "3.25h")
            if (Regex.IsMatch(input, @"^\d*\.?\d+\s*[H]?$"))
            {
                return ParseDecimalHours(input.Replace("H", "").Trim());
            }

            // 3. Handle compact format (e.g., "1430", "0930")
            if (Regex.IsMatch(input, @"^\d{3,4}$"))
            {
                return ParseCompactFormat(input);
            }

            // 4. Handle standard time formats with AM/PM
            return ParseStandardTime(input);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Invalid time format: {timeString}. " +
                                      "Supported formats: HH:MM, HH:MM:SS, HH:MM AM/PM, +XhYm, H.Hh", ex);
        }
    }
    public static T RandomFromArray<T>(T[] array) => RandomFromArray(array, out _);
    public static T RandomFromArray<T>(T[] array, out int index)
    {
        index = UnityEngine.Random.Range(0, array.Length);
        return array[index];
    }


    public static T RandomNonNullFromArray<T>(T[] array) where T : class => RandomNonNullFromArray(array, out _);
    public static T RandomNonNullFromArray<T>(T[] array, out int index) where T : class
    {
        T[] nonNulls = new T[array.Length];
        index = -1;
        int tick = 0;
        for (int i = 0; i < array.Length; i++)
            if (array[i] != null) nonNulls[tick++] = array[i];  // lets hope some windows update doesn't bork this like it did with GTA:SA

        if (nonNulls.Length == 0)
            return null;

        index = UnityEngine.Random.Range(0, array.Length);
        return nonNulls[index];
    }

    public static T RandomFromList<T>(List<T> list) => RandomFromList(list, out _);
    public static T RandomFromList<T>(List<T> list, out int index)
    {
        index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    public static T RandomNonNullFromList<T>(List<T> list) where T : class =>
        RandomNonNullFromList(list, out _);

    public static T RandomNonNullFromList<T>(List<T> list, out int index) where T : class
    {
        List<T> nonNulls = new List<T>();
        List<int> originalIndices = new List<int>();

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                nonNulls.Add(list[i]);
                originalIndices.Add(i);
            }
        }

        if (nonNulls.Count == 0)
        {
            index = -1;
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, nonNulls.Count);
        index = originalIndices[randomIndex];   
        return nonNulls[randomIndex];
    }

    public static Vector2 Vect2OneHalved => new(0.5f, 0.5f);

    private static long ParseRelativeTime(string relativeTime)
    {
        var pattern = new Regex(@"(?:([\d.]+)H)?\s*(?:([\d.]+)M)?", RegexOptions.IgnoreCase);
        var match = pattern.Match(relativeTime);

        long seconds = 0;
        if (match.Success)
        {
            if (match.Groups[1].Success)
            {
                seconds += (long)(double.Parse(match.Groups[1].Value) * 3600);
            }
            if (match.Groups[2].Success)
            {
                seconds += (long)(double.Parse(match.Groups[2].Value) * 60);
            }
        }
        else
        {
            // Try colon format for relative time (e.g., "+2:30")
            string[] parts = relativeTime.Split(':');
            if (parts.Length >= 2)
            {
                seconds += int.Parse(parts[0]) * 3600L;
                seconds += int.Parse(parts[1]) * 60L;
                if (parts.Length == 3)
                {
                    seconds += int.Parse(parts[2]);
                }
            }
        }

        return seconds;
    }

    private static long ParseDecimalHours(string decimalStr)
    {
        double decimalHours = double.Parse(decimalStr);
        return (long)(decimalHours * 3600);
    }

    private static long ParseCompactFormat(string compact)
    {
        // Pad with leading zero if needed
        if (compact.Length == 3)
        {
            compact = "0" + compact;
        }

        int hours = int.Parse(compact.Substring(0, 2));
        int minutes = int.Parse(compact.Substring(2, 2));

        ValidateTimeComponents(hours, minutes, 0);
        return hours * 3600L + minutes * 60L;
    }

    private static long ParseStandardTime(string timeStr)
    {
        // Extract AM/PM if present
        bool isPM = timeStr.Contains("PM");
        bool isAM = timeStr.Contains("AM");

        // Remove AM/PM for parsing
        string cleanTime = timeStr.Replace("AM", "").Replace("PM", "").Trim();

        // Split by colon or period
        string[] parts = cleanTime.Split(':', '.');
        if (parts.Length < 2)
        {
            throw new ArgumentException("Invalid time format");
        }

        int hours = int.Parse(parts[0]);
        int minutes = int.Parse(parts[1]);
        int seconds = (parts.Length >= 3) ? int.Parse(parts[2]) : 0;

        // Handle 12-hour format conversion
        if (isPM && hours != 12)
        {
            hours += 12;
        }
        else if (isAM && hours == 12)
        {
            hours = 0;
        }

        // Handle 24-hour format where hours might be > 12 without AM/PM
        if (!isAM && !isPM && hours <= 12 && Regex.IsMatch(timeStr, @"\d"))
        {
            // This is ambiguous - assume 24-hour format for values > 12, otherwise be explicit
            if (hours < 12 && timeStr.ToLower().Contains("p"))
            {
                hours += 12;
            }
        }

        ValidateTimeComponents(hours, minutes, seconds);
        return hours * 3600L + minutes * 60L + seconds;
    }

    private static void ValidateTimeComponents(int hours, int minutes, int seconds)
    {
        if (hours < 0 || hours > 23)
        {
            throw new ArgumentException("Hours must be between 0 and 23");
        }
        if (minutes < 0 || minutes > 59)
        {
            throw new ArgumentException("Minutes must be between 0 and 59");
        }
        if (seconds < 0 || seconds > 59)
        {
            throw new ArgumentException("Seconds must be between 0 and 59");
        }
    }

    /// <summary>
    /// Utility method to format seconds back to readable time
    /// </summary>
    public static string SecondsToTimeString(long seconds)
    {
        long hours = seconds / 3600;
        long minutes = (seconds % 3600) / 60;
        long secs = seconds % 60;

        return $"{hours:D2}:{minutes:D2}:{secs:D2}";
    }

    public static string SecondsToTimeString(float seconds) => SecondsToTimeString((long)seconds);

    public static void PlaySound(this AudioSource audioSource, AudioClip[] soundList)
    {
        if (soundList != null && soundList.Length > 0 && audioSource != null)
        {
            audioSource.PlayOneShot(soundList[UnityEngine.Random.Range(0, soundList.Length)]);
        }
    }

    public static T FindComponentInChildren<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
            return component;

        foreach (Transform child in gameObject.transform)
        {
            component = FindComponentInChildren<T>(child.gameObject);
            if (component != null)
                return component;
        }

        return null;
    }

    public static List<T> FindComponentsInChildren<T>(GameObject gameObject) where T : Component
    {
        List<T> components = new List<T>();
        FindComponentsInChildrenRecursive(gameObject, components);
        return components;
    }

    private static void FindComponentsInChildrenRecursive<T>(GameObject gameObject, List<T> components) where T : Component
    {
        T[] currentComponents = gameObject.GetComponents<T>();
        components.AddRange(currentComponents);

        foreach (Transform child in gameObject.transform)
        {
            FindComponentsInChildrenRecursive(child.gameObject, components);
        }
    }

    public static int NonNullItems<T>(T[] tArray)
    {
        if (tArray == null)
        {
            return 0;
        }

        int count = 0;
        foreach (T item in tArray)
        {
            if (item != null)
            {
                count++;
            }
        }
        return count;
    }

    public static List<T> ShuffleList<T>(List<T> list)
    {
        List<T> shuffled = new List<T>(list);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffled.Count);
            T temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }
        return shuffled;
    }

    public static Color semiTransparent = new(255f, 255f, 255f, 0.3f);

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

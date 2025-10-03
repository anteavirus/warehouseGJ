using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using System.Text;

public class FileDataManipulator
{
    private string _filePath;
    private Type _dataType;

    // Constructor for general use
    public FileDataManipulator(string location, Type dataType)
    {
        _filePath = location;
        _dataType = dataType;
    }

    // Generic constructor for type inference
    public FileDataManipulator(string location, object data)
    {
        _filePath = location;
        _dataType = data.GetType();
    }

    // Save data with appropriate serialization based on data type
    public void SaveData(object data)
    {
        if (data == null)
        {
            Debug.LogError("Cannot save null data");
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (IsSettingsType(data.GetType()))
            {
                SaveAsJson(data);
            }
            else
            {
                SaveWithCompression(data);
            }

            Debug.Log($"Data successfully saved to: {_filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save data to {_filePath}: {ex.Message}");
        }
    }

    // Load data with appropriate deserialization
    public object LoadData()
    {
        if (!File.Exists(_filePath))
        {
            Debug.LogWarning($"File not found: {_filePath}");
            return null;
        }

        try
        {
            object data;

            if (IsSettingsType(_dataType))
            {
                data = LoadFromJson();
            }
            else
            {
                data = LoadWithDecompression();
            }

            Debug.Log($"Data successfully loaded from: {_filePath}");
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load data from {_filePath}: {ex.Message}");
            return null;
        }
    }

    // Generic version for easier usage
    public T LoadData<T>()
    {
        var result = LoadData();
        return result != null ? (T)result : default(T);
    }

    // Check if the type is considered "settings" (typically for JSON serialization)
    private bool IsSettingsType(Type type)
    {
        // Settings are typically configuration data that should be human-readable
        return type.Name.ToLower().Contains("settings") ||
               type.Name.ToLower().Contains("config") ||
               type.IsPrimitive ||
               type == typeof(string) ||
               IsSimpleSerializableType(type);
    }

    private bool IsSimpleSerializableType(Type type)
    {
        return type.IsSerializable &&
               !type.IsArray &&
               !type.IsGenericType &&
               type.Namespace != null &&
               (type.Namespace.StartsWith("System") || type.Namespace.StartsWith("UnityEngine"));
    }

    // Save as JSON for settings (human-readable)
    private void SaveAsJson(object data)
    {
        string json = JsonUtility.ToJson(data, true); // pretty print for readability
        File.WriteAllText(_filePath, json, Encoding.UTF8);
    }

    // Save with compression for save data (smaller file size)
    private void SaveWithCompression(object data)
    {
        // First serialize to JSON
        string json = JsonUtility.ToJson(data);

        // Convert to bytes and compress
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using (FileStream fileStream = new FileStream(_filePath, FileMode.Create))
        using (GZipStream compressionStream = new GZipStream(fileStream, CompressionMode.Compress))
        {
            compressionStream.Write(jsonBytes, 0, jsonBytes.Length);
        }
    }

    // Load from JSON for settings
    private object LoadFromJson()
    {
        string json = File.ReadAllText(_filePath, Encoding.UTF8);
        return JsonUtility.FromJson(json, _dataType);
    }

    // Load with decompression for save data
    private object LoadWithDecompression()
    {
        using (FileStream fileStream = new FileStream(_filePath, FileMode.Open))
        using (GZipStream decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (MemoryStream memoryStream = new MemoryStream())
        {
            decompressionStream.CopyTo(memoryStream);
            byte[] jsonBytes = memoryStream.ToArray();
            string json = Encoding.UTF8.GetString(jsonBytes);
            return JsonUtility.FromJson(json, _dataType);
        }
    }

    // Utility method to check if file exists
    public bool FileExists()
    {
        return File.Exists(_filePath);
    }

    // Utility method to delete file
    public void DeleteFile()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            Debug.Log($"File deleted: {_filePath}");
        }
    }

    // Static helper methods for common use cases

    // Settings should probably remain in PlayerPrefs though, no?
    public static FileDataManipulator ForSettings(string settingsName, object settingsData) 
    {
        string path = Path.Combine(Application.persistentDataPath, $"{settingsName}.json");
        return new FileDataManipulator(path, settingsData);
    }

    public static FileDataManipulator ForSaveData(string saveName, object saveData)
    {
        string path = Path.Combine(Application.persistentDataPath, "Saves", $"{saveName}.save");
        return new FileDataManipulator(path, saveData);
    }

    public static FileDataManipulator ForStreamingAssets(string relativePath, object data)
    {
        string path = Path.Combine(Application.streamingAssetsPath, relativePath);
        return new FileDataManipulator(path, data);
    }
}

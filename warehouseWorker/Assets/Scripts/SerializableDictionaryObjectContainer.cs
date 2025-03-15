using System.Collections.Generic;
using UnityEngine;

/// <summary> UnityObject SerializableDictionary Container. tl;dr dictionary that i can edit in unity and put in unity objects </summary>
public class SerializableDictionaryObjectContainer : MonoBehaviour
{
    [SerializeField]
    private SerializableDictionary<string, Object> stringDictionary = new();

    public Object Fetch(string key)
    {
        return stringDictionary.ToDictionary().TryGetValue(key, out Object value) ? value : null;
    }

    public void Set(string key, Object value)
    {
        foreach (var item in stringDictionary.pairs)
        {
            if (item.Key == key)
            {
                item.Value = value;
                return;
            }
        }
        Debug.Log("Method Set has failed in SerializableDictionaryContainer.");
    }
}


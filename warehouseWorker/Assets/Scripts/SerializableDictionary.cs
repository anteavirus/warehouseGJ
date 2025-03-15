using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableDictionary<XValue, YValue>
{
    [System.Serializable]
    public class SerializableKeyValuePair
    {
        public XValue Key;
        public YValue Value;
    }

    [SerializeField]
    public List<SerializableKeyValuePair> pairs = new();

    public Dictionary<XValue, YValue> ToDictionary()
    {
        Dictionary<XValue, YValue> dictionary = new();
        foreach (var pair in pairs)
        {
            dictionary[pair.Key] = pair.Value;
        }
        return dictionary;
    }

    public bool TryGetValue(XValue key, out YValue value)
    {
        value = default;
        foreach (var pair in pairs)
        {
            if (pair.Key.Equals(key))
            {
                value = pair.Value;
                return true;
            }
        }
        return false;
    }

    public void SetValue(XValue key, YValue value)
    {
        foreach (var pair in pairs)
        {
            if (key.Equals(pair.Key))
            {
                pair.Value = value;
                return;
            }
        }
        Debug.LogWarning($"Serializable Dictionary did not find any {key} in pairs.");
    }

    /// <returns> Success / Failure (has this have key? yes? then fail.) </returns>
    public bool AddValue(XValue key, YValue value)
    {
        if (TryGetValue(key, out var _))
        {
            return false;
        }
        SerializableKeyValuePair ass = new();
        ass.Key = key; ass.Value = value;
        pairs.Add( ass );
        return true;
    }

    public IEnumerator<KeyValuePair<XValue, YValue>> GetEnumerator()
    {
        foreach (var pair in pairs)
        {
            yield return new KeyValuePair<XValue, YValue>(pair.Key, pair.Value);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenericManager<T> : MonoBehaviour where T : GenericManager<T>
{
    public static T Instance;

    public virtual void Initialize()
    {
        if (Instance == null || Instance == this as T)  // Yeah, it probably shouldn't just go harakiri cuz they found Instance and it was them.
        {
            Instance = this as T;
        }
        else
        {
            Instance.enabled = false;
            Debug.LogError($"{transform.name}: I'm reporting myself as a duplicate Instance! My parent is {transform?.parent?.name ?? "kinda homeless right now"}. I will turn myself off. Original Instance is {Instance.name} and their parent is {Instance.transform?.parent?.name ?? "kinda homeless right now"}");
            return;
        }

        // Initialize.
    }
}

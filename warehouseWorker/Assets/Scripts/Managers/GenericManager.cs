using UnityEngine;

/// <summary>
/// Lightweight MonoBehaviour singleton base.
/// </summary>
/// <typeparam name="T">The concrete manager type.</typeparam>
public class GenericManager<T> : MonoBehaviour where T : GenericManager<T>
{
    /// <summary>Static singleton reference. Set during Initialize().</summary>
    public static T Instance;

    /// <summary>
    /// Override this to perform setup. Called by MasterManager (or manually).
    /// Base implementation handles the singleton — call base.Initialize() first.
    /// </summary>
    public virtual void Initialize()
    {
        if (Instance == null || Instance == this as T)
        {
            Instance = this as T;
        }
        else
        {
            Instance.enabled = false;
            Debug.LogError(
                $"{transform.name}: Duplicate Instance detected! " +
                $"My parent: {transform?.parent?.name ?? "orphan"}. " +
                $"Original: {Instance.name} (parent: {Instance.transform?.parent?.name ?? "orphan"}). " +
                $"Disabling self.");
        }
    }
}

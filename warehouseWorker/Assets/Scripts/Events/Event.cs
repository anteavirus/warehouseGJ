using UnityEngine;

public class Event : MonoBehaviour
{
    public float duration;
    public bool isActive = false;

    public virtual void StartEvent()
    {
        isActive = true;
        Debug.Log($"Event {gameObject.name} started");
    }

    public virtual void EndEvent()
    {
        isActive = false;
        Debug.Log($"Event {gameObject.name} ended");
    }

    public virtual void UpdateEvent()
    {
        // This method will be called every frame while the event is active
    }
}

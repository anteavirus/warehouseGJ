using UnityEngine;

public class Event : MonoBehaviour
{
    public string eventName;
    public float duration;
    public bool isActive = false;

    public virtual void StartEvent()
    {
        isActive = true;
        Debug.Log($"Event {eventName} started");
    }

    public virtual void EndEvent()
    {
        isActive = false;
        Debug.Log($"Event {eventName} ended");
    }

    public virtual void UpdateEvent()
    {
        // This method will be called every frame while the event is active
    }
}

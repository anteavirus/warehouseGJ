using Mirror;
using UnityEngine;

public class Event : NetworkBehaviour
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
        // This method will be called every frame while the event is active (server only)
    }

    /// <summary>Called on clients so they run StartEvent (lights, camera, audio). Server already ran StartEvent in GameManager.</summary>
    [ClientRpc]
    public void RpcStartEvent()
    {
        if (!isServer)
            StartEvent();
    }

    /// <summary>Called on clients so they run EndEvent. Server will call EndEvent locally.</summary>
    [ClientRpc]
    public void RpcEndEvent()
    {
        if (!isServer)
            EndEvent();
    }
}

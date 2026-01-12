using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeetScript : NetworkBehaviour
{
    public bool isGrounded;
    public List<Collider> objects = new List<Collider>(16);

    public void CleanUp()
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] == null)
            {
                objects.RemoveAt(i);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        // Check if layer is 3 (grass) or 6 (interactable)
        if ((other.gameObject.layer == LayerMask.NameToLayer("Grass") || other.gameObject.layer == LayerMask.NameToLayer("Draggable") || other.gameObject.layer == LayerMask.NameToLayer("Interactable")) &&
            !objects.Contains(other))
        {
            objects.Add(other);
            isGrounded = true;
        }
    }
    // TODO: beg PlayerController for ground instead, dumbass
    private void OnTriggerExit(Collider other)
    {
        CleanUp();
        if ((other.gameObject.layer == LayerMask.NameToLayer("Grass") || other.gameObject.layer == LayerMask.NameToLayer("Draggable") || other.gameObject.layer == LayerMask.NameToLayer("Interactable")) &&
            objects.Contains(other))
        {
            objects.Remove(other);
        }

        if (objects.Count == 0)
        {
            isGrounded = false;
        }
    }
}

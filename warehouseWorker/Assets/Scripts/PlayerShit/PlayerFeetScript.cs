using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeetScript : MonoBehaviour
{
    public bool isGrounded;
    public List<Collider> objects = new List<Collider>(16);
    public PlayerController controller;

    private void Awake()
    {
        if (controller == null) 
            controller = GetComponentInParent<PlayerController>();
    }

    public void CleanUp()
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i].IsTrulyNull())
            {
                objects.RemoveAt(i);
            }
        }

        if (objects.Count == 0)
        {
            isGrounded = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger || !other.enabled) return;
        if (controller.groundLayer.ContainsLayer(other.gameObject.layer) &&
            !objects.Contains(other))
        {
            objects.Add(other);
            isGrounded = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CleanUp();
        if (controller.groundLayer.ContainsLayer(other.gameObject.layer) &&
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

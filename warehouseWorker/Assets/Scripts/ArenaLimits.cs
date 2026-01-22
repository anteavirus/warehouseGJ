using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaLimits : MonoBehaviour
{
    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Item>(out var _))
        {
            Destroy(other.gameObject);
        }

        else if (other.TryGetComponent<PlayerController>(out var plr))
        {
            plr.transform.SetPositionAndRotation(OrdersManager.Instance.spawnPosition.position, transform.rotation);   
        }
        else
        {
            Debug.LogWarning($"Unmonitored object left the Arena Limits: {other.name}");
        }
    }
}

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

        if (other.TryGetComponent<PlayerController>(out var plr))
        {
            plr.transform.SetPositionAndRotation(GameManager.Instance.spawnPosition.position, transform.rotation);   
        }
    }
}

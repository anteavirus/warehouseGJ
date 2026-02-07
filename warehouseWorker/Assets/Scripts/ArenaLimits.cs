using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaLimits : NetworkBehaviour
{
    private void OnTriggerExit(Collider other)
    {
        if (!isServer) return;

        if (other.TryGetComponent<Item>(out var _))
        {
            NetworkServer.Destroy(other.gameObject);
        }
        else if (other.TryGetComponent<PlayerController>(out var plr))
        {
            var backHere = GameObject.Find("Player Spawn")?.transform.position ?? Vector3.zero;

            // Ask the player to teleport themselves
            plr.CmdRequestTeleport(backHere, transform.rotation);
        }
    }

    [Server]
    void TeleportPlayer(PlayerController player, Vector3 position, Quaternion rotation)
    {
        player.transform.SetPositionAndRotation(position, rotation);

    }

    [TargetRpc]
    void TargetTeleportPlayer(NetworkConnection target, Vector3 position, Quaternion rotation)
    {
        var localPlayer = target.identity.gameObject.GetComponent<PlayerController>();
        if (localPlayer != null)
        {
            localPlayer.transform.SetPositionAndRotation(position, rotation);
        }
    }
}

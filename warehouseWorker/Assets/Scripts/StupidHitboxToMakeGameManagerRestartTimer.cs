using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StupidHitboxToMakeGameManagerRestartTimer : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.gameStarted && gm.setdownItem)
        {
            gm.ResetTimer();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StupidHitboxToMakeGameManagerRestartTimer : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.startGame && gm.setdownItem)
        {
            gm.ResetTimer();
        }
    }
}

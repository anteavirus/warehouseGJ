using Mirror;
using UnityEngine;

public class DestroySelfLikeSIGMA : NetworkBehaviour
{
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}

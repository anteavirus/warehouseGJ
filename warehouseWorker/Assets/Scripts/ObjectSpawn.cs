using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// class that can be "GetComponent"'d, that's it. Mostly to append to empty gameObjects, take info, create gameObject and purge this spawner
public class ObjectSpawn : MonoBehaviour
{
    public GameObject objectToSpawnOnMe;    
    public int amount = 1;
    public int range = 0;

    // Should I make a "onGizmosDraw"? I'll leave a comment here until I'll need to, because right now, the random spawn of clutter should - in theory - help me instead.
}
 
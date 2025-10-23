using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorMover : Item
{
    public DeliveryArea area;
    public bool iOpenDoor;
    private void Start()
    {
        
        isPickupable = false;
    }
    public override void OnPickup(Transform holder)
    {
        return; // fuck you 
    }
    public override void OnUse(GameObject user)
    {
        area.MoveDoors(iOpenDoor);
    }
}

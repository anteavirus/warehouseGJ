using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowShift : Item
{
    public DeliveryArea area;
    public bool iShiftLeft;
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
        area.ShiftSelection(!iShiftLeft);
    }
}

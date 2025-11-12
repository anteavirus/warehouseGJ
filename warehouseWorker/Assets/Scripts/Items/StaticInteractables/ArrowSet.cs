using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowSet : Item
{
    public ArrowControllableInt area = new(); // hack, no reason to use monobehavior on this other than to assign values beforehand
    public int selection;
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
        area.SetSelection(selection);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenericUIButton : Item
{
    public Action OnUseAction;
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
        OnUseAction?.Invoke();
    }
}

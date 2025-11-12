using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectronicItem : Item
{
    public bool isBroken;
    
    // TODO: implement more, this is as much of a nothingburger as it gets
    public override void OnUse(GameObject user)
    {
        if (isBroken) return;
        base.OnUse(user);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowControllableInt
{
    public ArrowShift left, right;
    public int selection = 0;
    public int size = 10;
    public System.Action<bool> OnSelectionChanged;
    public void ShiftSelection(bool shiftLeft)
    {
        selection = (selection + (shiftLeft ? -1 : 1) + size) % size;
        OnSelectionChanged?.Invoke(shiftLeft);
    }

    public void SetSelection(int selection) 
    {
        this.selection = Mathf.Clamp(selection, 0, size);
        OnSelectionChanged?.Invoke(false); // must not matter
    } 
}

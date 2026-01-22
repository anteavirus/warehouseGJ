using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Making this a network behaviour incase. Everything fucking comes crashing down because of prefabs spawning themselves via these blank behaviours or anything I don't know at this point I'm not that interested in the project, maybe the gamejam just drained the shit outta me. Mentally I look at this project and just think "Fucking hell this is going to be one fuck up to clean up"
public class Blank : MonoBehaviour
{
    static readonly string Comment = "Fucking Unity keeps on nagging about how adding MonoBehavior in scripts is dangerous and may cause memory leaks!\n" +
        "Well duh, that's why I'm doing this, stupid! Can't have a computer outsmart me here, now can I?";    
    public static string ReturnComment() => Comment;
}

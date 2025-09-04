using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blank : MonoBehaviour
{
    static readonly string Comment = "Fucking Unity keeps on nagging about how adding MonoBehavior in scripts is dangerous and may cause memory leaks!\n" +
        "Well duh, that's why I'm doing this, stupid! Can't have a computer outsmart me here, now can I?";    
    public static string ReturnComment() => Comment;
}

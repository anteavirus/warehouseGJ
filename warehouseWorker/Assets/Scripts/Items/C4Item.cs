using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class C4Item : Item
{
    [HideInInspector] public C4Event parentEvent;
    AudioSource source;

    public bool armed;
    private void Start()
    {
        source = GetComponent<AudioSource>();
        if (armed && source.clip != null) source.Play();
    }

    // Do we make the item tick up? Does the C4 event just SPAWN these two and goes ahead and thinks of another event to spawn? I don't know.
}
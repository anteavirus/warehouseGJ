using System.Collections.Generic;
using UnityEngine;
using static SettingsManager;

public class GroundSurface : MonoBehaviour
{
    public SurfaceType surfaceType;
}

public enum SurfaceType { Concrete, Metal, Wood, Water, Rubber }

[System.Serializable]
public class SurfaceSound
{
    public SurfaceType surface;
    public AudioClip[] footstepSounds;
    public AudioClip[] landingSounds;
}
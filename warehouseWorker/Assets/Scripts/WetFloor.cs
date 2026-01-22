using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WetFloor : MonoBehaviour
{
    PhysicMaterial physMaterialOriginal;
    Material materialOriginal;
    GroundSurface groundSurface;
    SurfaceType type;

    public PhysicMaterial physicMaterial;
    public Material material;
    public float timeLeft = 15;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            materialOriginal = rend.material;
            rend.material = material;
        }

        Collider coll = GetComponent<Collider>();
        if (coll != null)
        {
            physMaterialOriginal = coll.material;
            coll.material = physicMaterial;
        }

        groundSurface = GetComponent<GroundSurface>();
        if (groundSurface != null)
        {
            type = groundSurface.surfaceType;
            groundSurface.surfaceType = SurfaceType.Water;
        }
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null && materialOriginal != null)
                rend.material = materialOriginal;

            Collider coll = GetComponent<Collider>();
            if (coll != null && physMaterialOriginal != null)
                coll.material = physMaterialOriginal;

            if (groundSurface != null)
                groundSurface.surfaceType = type;

            Destroy(this);
        }
    }
}

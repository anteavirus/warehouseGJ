using System.Collections;
using UnityEngine;

public class WetFloorEvent : Event
{
    public PhysicMaterial wetPhysicMaterial;
    public Material wetMaterial;

    int affectedLayer;

    public override void StartEvent()
    {
        affectedLayer = LayerMask.NameToLayer("Grass");
        base.StartEvent();
        ApplyWetEffectToAllFloors();
        StartCoroutine(EventTimer());
    }

    void ApplyWetEffectToAllFloors()
    {
        GroundSurface[] allFloors = FindObjectsOfType<GroundSurface>();

        foreach (GroundSurface floor in allFloors)
        {
            if (!floor.TryGetComponent<WetFloor>(out var wetFloor) && floor.gameObject.layer == affectedLayer)
            {
                wetFloor = floor.gameObject.AddComponent<WetFloor>();
                wetFloor.physicMaterial = wetPhysicMaterial;
                wetFloor.material = wetMaterial;
                wetFloor.enabled = true;
            }
        }
    }

    IEnumerator EventTimer()
    {
        yield return new WaitForSeconds(duration);
        EndEvent();
    }

    public override void EndEvent()
    {
        base.EndEvent();

    }
}

using System.Collections;
using UnityEngine;

public class WetFloorEvent : Event
{
    public PhysicMaterial wetPhysicMaterial;
    public Material wetMaterial;

    public override void StartEvent()
    {
        base.StartEvent();
        ApplyWetEffectToAllFloors();
        StartCoroutine(EventTimer());
    }

    void ApplyWetEffectToAllFloors()
    {
        GroundSurface[] allFloors = FindObjectsOfType<GroundSurface>();

        foreach (GroundSurface floor in allFloors)
        {
            if (!floor.TryGetComponent<WetFloor>(out var wetFloor))
            {
                wetFloor = floor.gameObject.AddComponent<WetFloor>();
                wetFloor.physicMaterial = wetPhysicMaterial;
                wetFloor.material = wetMaterial;
            }

            wetFloor.timeLeft = duration;

            wetFloor.enabled = true;
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

using UnityEngine;

public class C4Event : Event
{
    [Header("Item References")]
    public C4Item c4Prefab;
    public C4Item disarmedC4Prefab;

    [Header("Spawn Points")]
    public Transform c4SpawnPoint;

    public C4Item activeC4;

    public override void StartEvent()
    {
        base.StartEvent();
        SpawnItems();
    }

    private void SpawnItems()
    {
        if (c4SpawnPoint == null)
            c4SpawnPoint = OrdersManager.Instance.spawnPosition;

        if (c4Prefab && c4SpawnPoint)
        {
            activeC4 = Instantiate(c4Prefab, c4SpawnPoint.position, Quaternion.identity);
            activeC4.parentEvent = this;
        }
    }
}

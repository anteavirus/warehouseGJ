using UnityEngine;

public class C4Event : Event
{
    [Header("Item References")]
    public C4Item c4Prefab;
    public C4Item disarmedC4Prefab;
    public ItemPliers PrefabPlier;

    [Header("Spawn Points")]
    public Transform c4SpawnPoint;
    public Transform SpawnPointPlier;

    public C4Item activeC4;
    private ItemPliers activePlier;

    public override void StartEvent()
    {
        base.StartEvent();
        SpawnItems();
    }

    private void SpawnItems()
    {
        if (c4SpawnPoint == null)
            c4SpawnPoint = GameManager.Instance.spawnPosition;
        if (SpawnPointPlier == null)
            SpawnPointPlier = GameManager.Instance.spawnPosition;

        if (c4Prefab && c4SpawnPoint)
        {
            activeC4 = Instantiate(c4Prefab, c4SpawnPoint.position, Quaternion.identity);
            activeC4.parentEvent = this;
        }

        if (PrefabPlier && SpawnPointPlier)
        {
            activePlier = Instantiate(PrefabPlier, SpawnPointPlier.position, Quaternion.identity);
            activePlier.parentEvent = this;
        }
    }
}

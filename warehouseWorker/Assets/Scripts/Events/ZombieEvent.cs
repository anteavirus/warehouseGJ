using UnityEngine;

public class ZombieEvent : Event
{
    [Header("Zombie Settings")]
    public GameObject zombiePrefab;
    public BoxCollider spawnArea;
    public int numberOfZombies = 3;

    [Header("Weapon Settings")]
    public GameObject[] throwableWeaponPrefabs;
    public int weaponsToSpawn = 2;
    public Transform[] weaponSpawnPoints;

    public override void StartEvent()
    {
        base.StartEvent();
        SpawnZombies();
        SpawnWeapons();
    }

    private void SpawnZombies()
    {
        if (spawnArea == null) spawnArea = GameObject.Find("ZombieSpawn").GetComponent<BoxCollider>();
        if (spawnArea == null || zombiePrefab == null)
        {
            Debug.LogError("Zombie spawn settings missing!");
            return;
        }

        for (int i = 0; i < numberOfZombies; i++)
        {
            Vector3 spawnPos = GetRandomPositionInBox(spawnArea.bounds);
            Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
        }
    }

    // TODO: make it check if the position is not inside kinetic rigidbodies, if it is in one at all; just. not inside objects, at least not static
    private Vector3 GetRandomPositionInBox(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y, // Spawn at same height as collider
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    private void SpawnWeapons()
    {
        if (throwableWeaponPrefabs.Length == 0 || weaponsToSpawn <= 0) return;

        for (int i = 0; i < weaponsToSpawn; i++)
        {
            GameObject weaponPrefab = throwableWeaponPrefabs[Random.Range(0, throwableWeaponPrefabs.Length)];
            Vector3 spawnPos = weaponSpawnPoints.Length > 0
                ? weaponSpawnPoints[Random.Range(0, weaponSpawnPoints.Length)].position
                : GameManager.Instance.spawnPosition.position;

            Instantiate(weaponPrefab, spawnPos, Quaternion.identity);
        }
    }
}

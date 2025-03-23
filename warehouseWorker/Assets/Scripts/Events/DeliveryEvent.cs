using UnityEngine;
using System.Collections.Generic;

public class DeliveryEvent : Event
{
    [Header("Delivery Settings")]
    public int decoysToSpawn = 5;
    public float spawnRadius = 5f;
    public Transform spawnCenter;
    public List<Item> decoyItems = new List<Item>();

    private List<Item> spawnedDecoys = new List<Item>();
    private Item _mainItem;
    public Item MainItem => _mainItem;

    public override void StartEvent()
    {
        base.StartEvent();
        SelectMainItem();
        SpawnDecoys();
    }

    private void SelectMainItem()
    {
        var validItems = GameManager.Instance.itemTemplates;

        _mainItem = validItems[Random.Range(0, validItems.Count)];
        Debug.Log($"Main delivery item selected: {_mainItem.name}");
    }

    private void SpawnDecoys()
    {
        for (int i = 0; i < decoysToSpawn; i++)
        {
            if (decoyItems.Count == 0) break;

            Item decoy = decoyItems[Random.Range(0, decoyItems.Count)];
            Vector3 spawnPos = spawnCenter.position + Random.insideUnitSphere * spawnRadius;
            spawnPos.y = spawnCenter.position.y;

            Item newDecoy = Instantiate(decoy, spawnPos, Quaternion.identity);
            spawnedDecoys.Add(newDecoy);
        }
    }

    public override void EndEvent()
    {
        foreach (Item decoy in spawnedDecoys)
        {
            if (decoy != null) Destroy(decoy.gameObject);
        }
        base.EndEvent();
    }
}

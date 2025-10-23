using UnityEngine;
using System.Collections.Generic;

public class DeliveryEvent : Event
{
    [Header("Delivery Settings")]
    public int decoysToSpawn = 5;
    public float spawnRadius = 5f;
    public Transform spawnCenter;
    public List<GameObject> decoyItems = new List<GameObject>();

    private List<GameObject> spawnedDecoys = new List<GameObject>();
    private Item _mainItem;
    public Item MainItem => _mainItem;
    public GameObject talkingDeliveryItem;

    public override void StartEvent()
    {
        base.StartEvent();
        if (spawnCenter == null) spawnCenter = OrdersManager.Instance.spawnPosition;
        SelectMainItem();
        SpawnMainItem();
        SpawnDecoys();
        if (GameManager.Instance.talkingDeliveryItem == null)
            GameManager.Instance.talkingDeliveryItem = Instantiate(talkingDeliveryItem);
    }

    private void SpawnMainItem()
    {
        // Find the corresponding prefab from GameManager's items list
        GameObject mainItemPrefab = null;
        foreach (var itemPrefab in GameManager.Instance.itemTemplates)
        {
            if (itemPrefab.TryGetComponent<Item>(out var itemComponent) && itemComponent.ID == _mainItem.ID)
            {
                mainItemPrefab = itemPrefab.gameObject;
                break;
            }
        }

        if (mainItemPrefab == null)
        {
            Debug.LogError("Failed to find main item prefab!");
            return;
        }

        // TODO: do we want the item in the box, or just like that?
        var deliveryBox = Instantiate(UsefulStuffs.RandomNonNullFromList(OrdersManager.Instance.boxPrefabs), spawnCenter.position, Quaternion.identity);
        deliveryBox.GetComponent<Box>().containedItem = mainItemPrefab;
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

            GameObject decoy = decoyItems[Random.Range(0, decoyItems.Count)];
            Vector3 spawnPos = spawnCenter.position + Random.insideUnitSphere * spawnRadius;
            spawnPos.y = spawnCenter.position.y;

            var newDecoy = Instantiate(decoy, spawnPos, Quaternion.identity);
            spawnedDecoys.Add(newDecoy);
        }
    }

    public override void EndEvent()
    {
        foreach (GameObject decoy in spawnedDecoys)
        {
            if (decoy != null) Destroy(decoy.gameObject);
        }
        base.EndEvent();
    }
}

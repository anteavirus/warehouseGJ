using System.Collections.Generic;
using UnityEngine;

public class PlayerGrabbyScript : MonoBehaviour
{
    [Header("References")]
    public PlayerController controller;

    [Header("Indicator Settings")]
    public Sprite indicatorSprite;
    public float rotationSpeed = 90f;
    public float spriteScale = 1f;

    [Header("Refresh Settings")]
    public float raycastRefreshInterval = 0.1f;

    private List<object> nearbyObjects = new();
    private RotatingIndicator currentIndicator;
    private float lastRaycastTime;

    public object focusedTarget => GetFocusedTarget();
    public Item focusedItem => focusedTarget as Item;

    private void Update()
    {
        if (Time.time - lastRaycastTime >= raycastRefreshInterval)
        {
            RefreshFocus();
            lastRaycastTime = Time.time;
        }
    }

    private object GetFocusedTarget()
    {
        // Raycast takes priority
        if (Physics.Raycast(controller.playerCamera.transform.position,
            controller.playerCamera.transform.forward, out RaycastHit hit,
            controller.pickupRange, controller.interactableLayer))
        {
            if (hit.collider.TryGetComponent<Item>(out var item)) return item;
            if (hit.collider.TryGetComponent<StorageArea>(out var area)) return area;
        }

        // Fall back to nearest object
        CleanupNullObjects();
        return nearbyObjects.Count > 0 ? nearbyObjects : null;
    }

    private void RefreshFocus()
    {
        UpdateIndicator();
        controller.UpdateHints(false, focusedTarget != null);
    }

    private void OnTriggerEnter(Collider other)
    {
        var obj = GetInteractableObject(other);
        if (obj != null && !nearbyObjects.Contains(obj))
        {
            nearbyObjects.Add(obj);
            RefreshFocus();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var obj = GetInteractableObject(other);
        if (obj != null)
        {
            nearbyObjects.Remove(obj);
            RefreshFocus();
        }
    }

    private object GetInteractableObject(Collider collider)
    {
        if (collider.TryGetComponent<Item>(out var item)) return item;
        if (collider.TryGetComponent<StorageArea>(out var area)) return area;
        return null;
    }

    public Item Pop()
    {
        DisableIndicator();

        var target = focusedTarget;
        if (target == null) return null;

        if (target is Item item)
        {
            nearbyObjects.Remove(item);
            return item;
        }

        if (target is StorageArea area)
        {
            var spawned = area.CreateNewItemForPickup();
            return spawned?.GetComponent<Item>();
        }

        return null;
    }

    private void UpdateIndicator()
    {
        var target = focusedTarget;
        if (target == null)
        {
            DisableIndicator();
            return;
        }

        var targetTransform = GetTransform(target);
        if (targetTransform == null)
        {
            DisableIndicator();
            return;
        }

        if (currentIndicator == null || !currentIndicator.IsValid() ||
            currentIndicator.targetTransform != targetTransform)
        {
            DisableIndicator();
            CreateIndicator(targetTransform);
        }
    }

    private void CreateIndicator(Transform target)
    {
        if (indicatorSprite == null) return;

        var indicatorObj = new GameObject("RotatingIndicator");
        currentIndicator = indicatorObj.AddComponent<RotatingIndicator>();
        currentIndicator.Initialize(target, indicatorSprite, rotationSpeed, spriteScale);
    }

    private void DisableIndicator()
    {
        if (currentIndicator != null)
        {
            currentIndicator.Disable();
            currentIndicator = null;
        }
    }

    private Transform GetTransform(object obj)
    {
        return obj switch
        {
            Item item => item?.transform,
            StorageArea area => area?.transform,
            _ => null
        };
    }

    private void CleanupNullObjects()
    {
        nearbyObjects.RemoveAll(obj => obj == null);
    }

    private void OnDestroy()
    {
        DisableIndicator();
    }
}
